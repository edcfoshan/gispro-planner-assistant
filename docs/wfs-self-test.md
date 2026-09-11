# TianDiTu WFS Self-Test

Before changing TianDiTu WFS behavior in the add-in, run:

```powershell
python scripts/dev/verify_tianditu_wfs.py
```

What this verifies:

- whether TianDiTu WFS expects `BBOX` in `lat,lon,lat,lon` order
- whether default GML output encodes geometry as `lat,lon`
- whether GeoJSON output encodes geometry as `lon,lat`

Current expected result:

- `SwapXYFilter = TRUE`
- `BBOX order = lat,lon,lat,lon`
- `SwapXY = TRUE` when ArcGIS Pro uses the default GML output
- returned features are filtered by intersection with the input extent, not clipped to it

Only build and package the add-in after this script passes.
