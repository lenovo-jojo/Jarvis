"""Stub external I/O only; parser, encoder and state logic remain real."""
import sys
import types

for name, functions in {
    "memory_ipc": [],
    "dialogue_prompt": ["generate_dialogue", "generate_spotlight_dialogue"],
    "gossip": ["seed_if_needed", "spread", "observe_interaction", "get_context_for"],
}.items():
    stub = types.ModuleType(name)
    for function in functions:
        setattr(stub, function, lambda *args, **kwargs: None)
    sys.modules[name] = stub
