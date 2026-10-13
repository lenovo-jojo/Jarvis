"""Run the recovered assertion checks without external test dependencies."""
from pathlib import Path
import runpy
import sys
import unittest

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
runpy.run_path(str(root / "tests/conftest.py"))
checks = runpy.run_path(str(root / "tests/test_feeling_chatty.py"))
suite = unittest.TestSuite(unittest.FunctionTestCase(function, description=name)
                           for name, function in checks.items()
                           if name.startswith("test_") and callable(function))
result = unittest.TextTestRunner(verbosity=2).run(suite)
sys.exit(0 if result.wasSuccessful() else 1)
