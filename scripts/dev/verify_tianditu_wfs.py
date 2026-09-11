import argparse
import json
import re
import sys
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path


WFS_URL = "http://gisserver.tianditu.gov.cn/TDTService/wfs"
GML_NS = {"gml": "http://www.opengis.net/gml/3.2"}
PROJECT_ROOT = Path(__file__).resolve().parents[2]
DEFAULTS_PATH = PROJECT_ROOT / "GisProAddinWorkbench.AddIn" / "Modules" / "Imagery" / "ImageryCredentialDefaults.cs"


@dataclass(frozen=True)
class Case:
    name: str
    type_name: str
    lon_min: float
    lat_min: float
    lon_max: float
    lat_max: float

    @property
    def lonlat_bbox(self) -> str:
        return f"{self.lon_min},{self.lat_min},{self.lon_max},{self.lat_max}"

    @property
    def latlon_bbox(self) -> str:
        return f"{self.lat_min},{self.lon_min},{self.lat_max},{self.lon_max}"


CASES = [
    Case("AGNP-Zhongshan", "TDTService:AGNP", 113.21130424364665, 22.327804114523332, 113.55402208045687, 22.608409373104042),
    Case("BOUA-Zhongshan", "TDTService:BOUA", 113.21130424364665, 22.327804114523332, 113.55402208045687, 22.608409373104042),
    Case("HYDL-Zhongshan", "TDTService:HYDL", 113.21130424364665, 22.327804114523332, 113.55402208045687, 22.608409373104042),
]


def load_default_key() -> str:
    text = DEFAULTS_PATH.read_text(encoding="utf-8")
    anchor = text.find("TianDiTuKeys")
    if anchor < 0:
        raise RuntimeError(f"Unable to find TianDiTuKeys in {DEFAULTS_PATH}")

    match = re.search(r'"([0-9a-fA-F]{32})"', text[anchor:])
    if not match:
        raise RuntimeError(f"Unable to find a TianDiTu key in {DEFAULTS_PATH}")
    return match.group(1)


def request_wfs(params: dict[str, str]) -> str:
    query = urllib.parse.urlencode(params)
    with urllib.request.urlopen(f"{WFS_URL}?{query}", timeout=30) as response:
        return response.read().decode("utf-8", "ignore")


def fetch_gml(case: Case, bbox: str, key: str) -> tuple[int, str]:
    text = request_wfs(
        {
            "service": "WFS",
            "request": "GetFeature",
            "version": "2.0.0",
            "typeNames": case.type_name,
            "count": "5",
            "bbox": bbox,
            "tk": key,
        }
    )
    root = ET.fromstring(text)
    matched = int(root.attrib.get("numberMatched", "0"))
    return matched, text


def fetch_geojson(case: Case, bbox: str, key: str) -> dict:
    text = request_wfs(
        {
            "service": "WFS",
            "request": "GetFeature",
            "version": "2.0.0",
            "typeNames": case.type_name,
            "count": "5",
            "bbox": bbox,
            "outputFormat": "application/json",
            "tk": key,
        }
    )
    return json.loads(text)


def first_gml_pair(gml_text: str) -> tuple[float, float]:
    root = ET.fromstring(gml_text)
    pos = root.find(".//gml:pos", GML_NS)
    if pos is not None and pos.text:
        a, b = pos.text.strip().split()[:2]
        return float(a), float(b)

    pos_list = root.find(".//gml:posList", GML_NS)
    if pos_list is not None and pos_list.text:
        values = pos_list.text.strip().split()
        return float(values[0]), float(values[1])

    raise RuntimeError("No gml:pos or gml:posList was found in the GML response.")


def first_geojson_pair(obj: dict) -> tuple[float, float]:
    def walk(node):
        if isinstance(node, list) and node and isinstance(node[0], (int, float)):
            return float(node[0]), float(node[1])
        if isinstance(node, list):
            for item in node:
                pair = walk(item)
                if pair is not None:
                    return pair
        return None

    for feature in obj.get("features", []):
        pair = walk(feature.get("geometry", {}).get("coordinates"))
        if pair is not None:
            return pair

    raise RuntimeError("No coordinate pair was found in the GeoJSON response.")


def classify_axis(pair: tuple[float, float]) -> str:
    first, second = pair
    if abs(first) <= 90 and abs(second) > 90:
        return "lat,lon"
    if abs(first) > 90 and abs(second) <= 90:
        return "lon,lat"
    return "ambiguous"


def run_case(case: Case, key: str) -> list[str]:
    failures: list[str] = []
    lonlat_count, _ = fetch_gml(case, case.lonlat_bbox, key)
    latlon_count, latlon_gml = fetch_gml(case, case.latlon_bbox, key)
    gml_axis = classify_axis(first_gml_pair(latlon_gml))
    geojson_axis = classify_axis(first_geojson_pair(fetch_geojson(case, case.latlon_bbox, key)))

    print(f"[case] {case.name}")
    print(f"  lon,lat bbox -> matched {lonlat_count}")
    print(f"  lat,lon bbox -> matched {latlon_count}")
    print(f"  GML geometry axis -> {gml_axis}")
    print(f"  GeoJSON geometry axis -> {geojson_axis}")

    if latlon_count <= 0:
        failures.append(f"{case.name}: lat,lon bbox returned no features")
    if lonlat_count >= latlon_count:
        failures.append(f"{case.name}: lon,lat bbox was not stricter than lat,lon bbox")
    if gml_axis != "lat,lon":
        failures.append(f"{case.name}: expected GML geometry axis lat,lon but got {gml_axis}")
    if geojson_axis != "lon,lat":
        failures.append(f"{case.name}: expected GeoJSON geometry axis lon,lat but got {geojson_axis}")

    return failures


def main() -> int:
    parser = argparse.ArgumentParser(description="Repeatable TianDiTu WFS verification.")
    parser.add_argument("--key", help="Optional TianDiTu key. Falls back to ImageryCredentialDefaults.cs")
    args = parser.parse_args()

    key = args.key or load_default_key()
    all_failures: list[str] = []

    print("TianDiTu WFS verification")
    print(f"Service URL: {WFS_URL}")
    print(f"Cases: {len(CASES)}")

    for case in CASES:
        all_failures.extend(run_case(case, key))

    print()
    print("Recommended ArcGIS WFS settings for TianDiTu")
    print("  SwapXYFilter = TRUE")
    print("  BBOX order    = lat,lon,lat,lon")
    print("  SwapXY        = TRUE for default GML output")
    print("  Note          = WFS bbox filters by intersection; geometries are not clipped")

    if all_failures:
        print()
        print("Failures:")
        for failure in all_failures:
            print(f"  - {failure}")
        return 1

    print()
    print("All checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
