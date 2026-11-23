"""Permite ejecutar `python3 -m mente` directamente."""

from __future__ import annotations

import sys

from mente.servicio import main

if __name__ == "__main__":
    raise SystemExit(main())
