#!/usr/bin/env python3
"""dograc Desktop-to-Web Hybrid Launcher entry point."""
try:
    from velopack import VelopackApp
    VelopackApp.build().run()
except Exception:
    # Safely ignore in dev environments, tests, or when velopack native hooks are not active
    pass

from launcher.__main__ import main

if __name__ == "__main__":
    main()
