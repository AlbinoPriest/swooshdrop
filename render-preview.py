#!/usr/bin/env python3
"""Normalize an untrusted AirDrop icon or an accepted photo into a small PNG.

Separate process, resource limits, fixed format list, no remote fetches.
"""
import base64
import io
import json
import os
import resource
import subprocess
import sys
import tempfile
import warnings
from pathlib import Path

resource.setrlimit(resource.RLIMIT_AS, (512 * 1024 * 1024,) * 2)
resource.setrlimit(resource.RLIMIT_CPU, (5, 5))
resource.setrlimit(resource.RLIMIT_FSIZE, (100 * 1024 * 1024,) * 2)
from PIL import Image, ImageOps
Image.MAX_IMAGE_PIXELS = 25_000_000
warnings.simplefilter('error', Image.DecompressionBombWarning)

try:
    raw = sys.stdin.buffer.read(1_500_001)
    if len(raw) > 1_500_000:
        raise ValueError('request too large')
    request = json.loads(raw)
    with tempfile.TemporaryDirectory(prefix='windrop-preview-') as temporary:
        if 'image' in request:
            data = base64.b64decode(request['image'], validate=True)
            if len(data) > 1024 * 1024:
                raise ValueError('icon too large')
            source = io.BytesIO(data)
            formats = ['JPEG2000', 'JPEG', 'PNG']
            Image.MAX_IMAGE_PIXELS = 4_000_000
        else:
            path = Path(request['file']).resolve(strict=True)
            if path.stat().st_size > 100 * 1024 * 1024:
                raise ValueError('photo too large')
            if path.suffix.lower() in ('.heic', '.heif'):
                converted = Path(temporary) / 'preview.jpg'
                subprocess.run(['/usr/bin/heif-convert', '--quiet', '-q', '75', str(path), str(converted)],
                               check=True, timeout=5, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                source = str(converted)
            else:
                source = str(path)
            formats = ['JPEG', 'PNG', 'WEBP', 'BMP', 'GIF']
        with Image.open(source, formats=formats) as image:
            image = ImageOps.exif_transpose(image)
            image.thumbnail((440, 300), Image.Resampling.LANCZOS)
            buffer = io.BytesIO()
            image.convert('RGB').save(buffer, format='PNG')
            print(base64.b64encode(buffer.getvalue()).decode('ascii'))
except Exception:
    sys.exit(1)
