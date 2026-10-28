"""Reuse the actual SQL/Identity Office journey, independent of historical gate verdicts."""
from pathlib import Path
from types import SimpleNamespace
import os
import gate5d_browser
root=Path(__file__).resolve().parents[2]
evidence=root/'.audit-run/ui-v2/office'
evidence.mkdir(parents=True,exist_ok=True)
visuals=root/'.audit-run/ui-v2/screenshots'
visuals.mkdir(parents=True,exist_ok=True)
os.environ['STUDIO_VISUAL_DIR']=str(visuals)
gate5d_browser.browser(SimpleNamespace(ROOT=root.parent,FEATURE=root,EVIDENCE=evidence))
