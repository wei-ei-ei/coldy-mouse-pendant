# Reproduce the pixel cursor pack extraction from the author's installer exe.
# The exe embeds a ZIP (PK signature) whose cursor/EmbeddedCursorPack.zip holds
# the 17 animated .ani cursors + install.inf. We never run the exe.
# Usage: python tools/extract_pixel_pack.py <path-to-installer.exe> [out-dir]
import struct
import sys
import zipfile
import io
from pathlib import Path

exe = Path(sys.argv[1])
out = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).resolve().parent.parent / "assets" / "cursors" / "pixel"
data = exe.read_bytes()

start = data.find(b"PK\x03\x04")
eocd = data.rfind(b"PK\x05\x06")
if start < 0 or eocd < 0:
    sys.exit("no embedded zip found")
comment_len = struct.unpack_from("<H", data, eocd + 20)[0]
outer = zipfile.ZipFile(io.BytesIO(data[start:eocd + 22 + comment_len]))
inner_name = next(n for n in outer.namelist() if n.endswith("EmbeddedCursorPack.zip"))
inner = zipfile.ZipFile(io.BytesIO(outer.read(inner_name)))

out.mkdir(parents=True, exist_ok=True)
count = 0
for name in inner.namelist():
    if name.endswith("/"):
        continue
    target = out / Path(name).name
    target.write_bytes(inner.read(name))
    count += 1
print("extracted %d files -> %s" % (count, out))