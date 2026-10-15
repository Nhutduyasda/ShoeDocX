"""Read official compressed NuGet advisories directly; do not treat NU1900 as clean."""
import gzip
import json
from pathlib import Path
import re
import sys
import urllib.request


def fetch(url):
    assert url.startswith("https://api.nuget.org/"), "Unexpected advisory source"
    with urllib.request.urlopen(url, timeout=45) as response:
        data = response.read(32 * 1024 * 1024)
    if data.startswith(b"\x1f\x8b"):
        data = gzip.decompress(data)
    return json.loads(data)


def version(text):
    # NuGet/SemVer numeric core and prerelease ordering; fail closed on another syntax.
    match = re.fullmatch(r"(\d+(?:\.\d+){0,3})(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?", text)
    assert match, "Unsupported package version: " + text
    values = tuple(map(int, match[1].split(".")))
    prerelease = tuple((0, int(p)) if p.isdigit() else (1, p.lower()) for p in (match[2] or "").split("."))
    return values + (0,) * (4 - len(values)) + (0 if match[2] else 1, prerelease)


def affected(current, interval):
    # NuGet advisory data uses one mathematical interval per record.
    assert interval[0] in "[(" and interval[-1] in ")]", "Unsupported advisory range"
    parts = interval[1:-1].split(",")
    if len(parts) == 1:
        return version(current) == version(parts[0].strip())
    assert len(parts) == 2
    lower, upper = (p.strip() for p in parts)
    actual = version(current)
    return (not lower or actual > version(lower) or actual == version(lower) and interval[0] == "[") and (
        not upper or actual < version(upper) or actual == version(upper) and interval[-1] == "]")


def audit(root):
    index = fetch("https://api.nuget.org/v3/vulnerabilities/index.json")
    assert index and all(i.get("@updated") for i in index), "Missing advisory metadata"
    advisories = {}
    for item in index:
        for name, records in fetch(item["@id"]).items():
            advisories.setdefault(name, []).extend(records)
    packages = set()
    for relative in ("backend/ShoeExportInvoice.Api", "tests/ShoeExportInvoice.Tests", "tests/Gate4ADemoHost", "tests/Gate4B1DemoHost"):
        assets = root / relative / "obj/project.assets.json"
        if not assets.exists():
            if relative.endswith(("Gate4ADemoHost", "Gate4B1DemoHost")):
                continue
            raise AssertionError("Missing restored package assets")
        for identity, entry in json.loads(assets.read_text(encoding="utf-8"))["libraries"].items():
            if entry["type"] == "package":
                name, resolved = identity.split("/")
                packages.add((name.lower(), resolved))
    findings = []
    for name, resolved in sorted(packages):
        for record in advisories.get(name, []):
            if affected(resolved, record["versions"]):
                row = {"package": name, "version": resolved, "severity": ["low", "moderate", "high", "critical"][record["severity"]],
                       "affected_range": record["versions"], "url": record["url"]}
                if row not in findings:
                    findings.append(row)
    return {"source": "https://api.nuget.org/v3/vulnerabilities/index.json", "advisory_updates": index,
            "resolved_packages_checked": len(packages), "resolved_packages": sorted(packages), "findings": findings}


if __name__ == "__main__":
    report = audit(Path(sys.argv[1]).resolve())
    Path(sys.argv[2]).write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
