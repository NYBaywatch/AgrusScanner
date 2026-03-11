import json
import os
import urllib.request
import boto3
from datetime import datetime, timezone

dynamodb = boto3.resource("dynamodb")
table = dynamodb.Table(os.environ.get("TABLE_NAME", "agrus-scanner-telemetry"))

GITHUB_API_URL = "https://api.github.com/repos/NYBaywatch/AgrusScanner/releases/latest"


def lambda_handler(event, context):
    params = event.get("queryStringParameters") or {}
    version = params.get("v", "unknown")
    os_info = params.get("os", "unknown")

    now = datetime.now(timezone.utc)

    # Log the ping to DynamoDB
    try:
        table.put_item(Item={
            "date": now.strftime("%Y-%m-%d"),
            "timestamp": now.isoformat(),
            "version": version,
            "os": os_info,
        })
    except Exception:
        pass  # Don't fail the response if logging fails

    # Fetch latest release from GitHub
    try:
        req = urllib.request.Request(GITHUB_API_URL, headers={"User-Agent": "AgrusScanner-UpdateCheck/1.0"})
        with urllib.request.urlopen(req, timeout=5) as resp:
            release = json.loads(resp.read())

        tag = release.get("tag_name", "")
        html_url = release.get("html_url", f"https://github.com/NYBaywatch/AgrusScanner/releases/tag/{tag}")

        # Build direct MSI download URL from tag
        download_url = f"https://github.com/NYBaywatch/AgrusScanner/releases/download/{tag}/AgrusScanner-Setup.msi"

        return {
            "statusCode": 200,
            "headers": {"Content-Type": "application/json", "Access-Control-Allow-Origin": "*"},
            "body": json.dumps({
                "latest_version": tag.lstrip("vV"),
                "tag_name": tag,
                "download_url": download_url,
                "release_url": html_url,
            }),
        }
    except Exception:
        return {
            "statusCode": 200,
            "headers": {"Content-Type": "application/json", "Access-Control-Allow-Origin": "*"},
            "body": json.dumps({
                "latest_version": None,
                "error": "Could not fetch release info",
            }),
        }
