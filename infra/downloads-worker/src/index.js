// downloads.jpftech.com
//
//   GET /AgrusScanner-Setup-1.0.2.msi   stream from R2, count one download
//   GET /AgrusScanner-Setup.msi         rolling "latest" name, counted under its own key
//   GET /stats.json                     { direct: {file: n, ...}, direct_total, github_total, total }
//   GET /badge.json                     shields.io endpoint badge: combined installer download count
//   GET /badge-signatures.json          shields.io endpoint badge: signature package downloads
//   GET /signatures/latest.json         signature feed manifest (proxied from the GitHub "signatures"
//                                       release, cached 60 s). Counted as one "check-in" per fetch,
//                                       per day: installed apps poll this daily, so the daily number
//                                       approximates active installs.
//   GET /signatures/latest.agsig        signature package (proxied, cached 10 min). Counted per sigVersion.
//   GET /                               redirect to the Tools page
//
// Counting: one increment per completed GET of a .msi (HEAD and non-zero Range requests are
// not counted, so resumed/segmented downloads do not double count). KV increments are not
// atomic; under heavy concurrency a few counts can be lost, which is acceptable for a badge.

const ALLOWED_EXT = /\.(msi|exe|zip|agsig|json|txt)$/i;

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    const path = decodeURIComponent(url.pathname);

    if (path === "/" || path === "") {
      return Response.redirect(env.TOOLS_PAGE, 302);
    }
    if (path === "/stats.json") return stats(env, ctx);
    if (path === "/badge.json") return badge(env, ctx);
    if (path === "/badge-signatures.json" || path === "/badge-active.json") return signatureBadge(env);
    if (path === "/robots.txt") return new Response("User-agent: *\nDisallow: /\n", { headers: { "content-type": "text/plain" } });
    if (path.startsWith("/signatures/")) return signatures(path.slice("/signatures/".length), request, env, ctx);

    if (request.method !== "GET" && request.method !== "HEAD") {
      return new Response("Method not allowed", { status: 405, headers: { allow: "GET, HEAD" } });
    }
    const key = path.replace(/^\/+/, "");
    if (!key || key.includes("..") || !ALLOWED_EXT.test(key)) {
      return new Response("Not found", { status: 404 });
    }

    const rangeHeader = request.headers.get("range");
    const object = request.method === "HEAD"
      ? await env.BUCKET.head(key)
      : await env.BUCKET.get(key, { range: request.headers, onlyIf: request.headers });

    if (!object) return new Response("Not found", { status: 404 });

    const headers = new Headers();
    object.writeHttpMetadata(headers);
    headers.set("etag", object.httpEtag);
    headers.set("accept-ranges", "bytes");
    headers.set("cache-control", "public, max-age=3600");
    headers.set("x-content-type-options", "nosniff");
    if (!headers.get("content-type")) headers.set("content-type", "application/octet-stream");
    if (/\.msi$/i.test(key)) headers.set("content-disposition", `attachment; filename="${key.split("/").pop()}"`);

    if (request.method === "HEAD") {
      headers.set("content-length", String(object.size));
      return new Response(null, { status: 200, headers });
    }

    // Preconditions (If-None-Match etc.): R2 returns an object without a body.
    if (!("body" in object) || object.body === null) {
      return new Response(null, { status: 304, headers });
    }

    let status = 200;
    if (object.range && rangeHeader) {
      const { offset = 0, length = object.size - offset } = object.range;
      const end = offset + length - 1;
      headers.set("content-range", `bytes ${offset}-${end}/${object.size}`);
      headers.set("content-length", String(length));
      status = 206;
      if (offset === 0) ctx.waitUntil(bump(env, key));
    } else {
      headers.set("content-length", String(object.size));
      ctx.waitUntil(bump(env, key));
    }

    return new Response(object.body, { status, headers });
  }
};

// ── Signature feed ─────────────────────────────────────────────────────────────
const SIG_UPSTREAM = "https://github.com/NYBaywatch/AgrusScanner/releases/download/signatures/";

async function signatures(name, request, env, ctx) {
  if (name !== "latest.json" && name !== "latest.agsig") return new Response("Not found", { status: 404 });
  if (request.method !== "GET" && request.method !== "HEAD") return new Response("Method not allowed", { status: 405 });

  const cache = caches.default;
  const cacheKey = new Request(`https://downloads.jpftech.com/signatures/${name}`, { method: "GET" });
  let res = await cache.match(cacheKey);
  if (!res) {
    const upstream = await fetch(SIG_UPSTREAM + name, { headers: { "user-agent": "agrus-downloads-worker" }, redirect: "follow" });
    if (!upstream.ok) return new Response("Feed unavailable", { status: 502 });
    const body = await upstream.arrayBuffer();
    const headers = new Headers({
      "content-type": name.endsWith(".json") ? "application/json; charset=utf-8" : "application/octet-stream",
      "cache-control": name.endsWith(".json") ? "public, max-age=60" : "public, max-age=600",
      "access-control-allow-origin": "*",
      "x-content-type-options": "nosniff"
    });
    res = new Response(body, { status: 200, headers });
    ctx.waitUntil(cache.put(cacheKey, res.clone()));
  }
  if (request.method === "GET") ctx.waitUntil(bumpSignature(env, name, res.clone()));
  return request.method === "HEAD" ? new Response(null, { status: 200, headers: res.headers }) : res;
}

async function bumpSignature(env, name, res) {
  try {
    const day = new Date().toISOString().slice(0, 10);
    if (name === "latest.json") {
      await incr(env, `sigcheck:${day}`, 60 * 60 * 24 * 400);
      await incr(env, "sigcheck:total");
    } else {
      let ver = "unknown";
      try {
        const m = await (await caches.default.match(new Request("https://downloads.jpftech.com/signatures/latest.json")))?.json();
        if (m?.sig_version) ver = m.sig_version;
      } catch {}
      await incr(env, `sigdl:${ver}`);
      await incr(env, "sigdl:total");
    }
  } catch {}
}

async function incr(env, key, ttl) {
  const n = parseInt((await env.COUNTS.get(key)) || "0", 10) || 0;
  await env.COUNTS.put(key, String(n + 1), ttl ? { expirationTtl: ttl } : undefined);
}

async function signatureStats(env) {
  const today = new Date().toISOString().slice(0, 10);
  const yesterday = new Date(Date.now() - 86400000).toISOString().slice(0, 10);
  const [t, y, total, dlTotal] = await Promise.all([
    env.COUNTS.get(`sigcheck:${today}`), env.COUNTS.get(`sigcheck:${yesterday}`),
    env.COUNTS.get("sigcheck:total"), env.COUNTS.get("sigdl:total")
  ]);
  const byVersion = {};
  const list = await env.COUNTS.list({ prefix: "sigdl:" });
  for (const k of list.keys) if (k.name !== "sigdl:total") byVersion[k.name.slice(6)] = parseInt((await env.COUNTS.get(k.name)) || "0", 10) || 0;
  // last 14 days of check-ins
  const days = {};
  for (let i = 13; i >= 0; i--) {
    const d = new Date(Date.now() - i * 86400000).toISOString().slice(0, 10);
    days[d] = parseInt((await env.COUNTS.get(`sigcheck:${d}`)) || "0", 10) || 0;
  }
  return {
    checks_today: parseInt(t || "0", 10) || 0,
    checks_yesterday: parseInt(y || "0", 10) || 0,
    checks_total: parseInt(total || "0", 10) || 0,
    checks_by_day: days,
    package_downloads_total: parseInt(dlTotal || "0", 10) || 0,
    package_downloads_by_version: byVersion
  };
}

async function bump(env, key) {
  if (!/\.(msi|exe|zip)$/i.test(key)) return;
  try {
    const k = `dl:${key}`;
    const current = parseInt((await env.COUNTS.get(k)) || "0", 10) || 0;
    await env.COUNTS.put(k, String(current + 1));
    const day = new Date().toISOString().slice(0, 10);
    const dk = `day:${day}:${key}`;
    const dc = parseInt((await env.COUNTS.get(dk)) || "0", 10) || 0;
    await env.COUNTS.put(dk, String(dc + 1), { expirationTtl: 60 * 60 * 24 * 400 });
  } catch (e) {
    // counting must never break a download
  }
}

async function directCounts(env) {
  const list = await env.COUNTS.list({ prefix: "dl:" });
  const direct = {};
  let total = 0;
  for (const k of list.keys) {
    const n = parseInt((await env.COUNTS.get(k.name)) || "0", 10) || 0;
    direct[k.name.slice(3)] = n;
    total += n;
  }
  return { direct, total };
}

async function githubTotal(env) {
  // Cached for 10 minutes to stay well inside GitHub's unauthenticated rate limit.
  const cacheKey = "cache:github_total";
  const cached = await env.COUNTS.get(cacheKey, { type: "json" });
  if (cached && Date.now() - cached.at < 10 * 60 * 1000) return cached.total;
  try {
    const res = await fetch(`https://api.github.com/repos/${env.GITHUB_REPO}/releases?per_page=100`, {
      headers: { "user-agent": "agrus-downloads-worker", accept: "application/vnd.github+json" }
    });
    if (!res.ok) return cached?.total ?? 0;
    const releases = await res.json();
    let total = 0;
    // Installer downloads only: the "signatures" release is the feed, and --clobber resets its counts.
    for (const r of releases) {
      if (r.tag_name === "signatures") continue;
      for (const a of r.assets || []) total += a.download_count || 0;
    }
    await env.COUNTS.put(cacheKey, JSON.stringify({ total, at: Date.now() }));
    return total;
  } catch {
    return cached?.total ?? 0;
  }
}

async function stats(env) {
  const [{ direct, total: directTotal }, gh, sig] = await Promise.all([directCounts(env), githubTotal(env), signatureStats(env)]);
  const body = {
    direct,
    direct_total: directTotal,
    github_total: gh,
    total: directTotal + gh,
    signatures: sig,
    generated: new Date().toISOString()
  };
  return json(body, 60);
}

async function signatureBadge(env) {
  const sig = await signatureStats(env);
  return json({ schemaVersion: 1, label: "signature downloads", message: compact(sig.package_downloads_total), color: "2ea44f", cacheSeconds: 3600 }, 3600);
}

async function badge(env) {
  const [{ total: directTotal }, gh] = await Promise.all([directCounts(env), githubTotal(env)]);
  const total = directTotal + gh;
  return json({ schemaVersion: 1, label: "downloads", message: compact(total), color: "2ea44f", cacheSeconds: 300 }, 300);
}

function compact(n) {
  if (n >= 1_000_000) return (n / 1_000_000).toFixed(1).replace(/\.0$/, "") + "M";
  if (n >= 1_000) return (n / 1_000).toFixed(1).replace(/\.0$/, "") + "k";
  return String(n);
}

function json(obj, maxAge) {
  return new Response(JSON.stringify(obj, null, 2), {
    headers: {
      "content-type": "application/json; charset=utf-8",
      "cache-control": `public, max-age=${maxAge}`,
      "access-control-allow-origin": "*"
    }
  });
}
