import json5
from types import SimpleNamespace

def dict_to_simplenamespace(d):
    """
    Recursively converts a dictionary to a SimpleNamespace, including nested dictionaries.
    """
    if isinstance(d, dict):
        # Convert each dictionary value to SimpleNamespace
        return SimpleNamespace(**{k: dict_to_simplenamespace(v) for k, v in d.items()})
    elif isinstance(d, list):
        # If the value is a list, apply the conversion to each item in the list
        return [dict_to_simplenamespace(i) for i in d]
    else:
        # Return the value as is if it's not a dictionary or list
        return d

_data = None
with open("assets/config.json", 'r') as f:
    _data = json5.load(f)
cfg = dict_to_simplenamespace(_data)

import logging
logging.basicConfig(level=logging.DEBUG if cfg.DEBUG else logging.INFO,
                    format='{"level":"%(levelname)s", "data":{%(message)s}}')