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

import sys
import logging

_root_logger = logging.getLogger()
_root_logger.setLevel(logging.DEBUG if cfg.DEBUG else logging.INFO)

# Create a formatter
formatter = logging.Formatter("%(asctime)s - [%(levelname)s] - %(message)s")

class StdOutFilter:
    def __call__(self, log):
        return log.levelno < logging.WARNING

# Create a handler for stdout
stdout_handler = logging.StreamHandler(sys.stdout)
stdout_handler.setLevel(logging.DEBUG if cfg.DEBUG else logging.INFO) 
stdout_handler.setFormatter(formatter)
stdout_handler.addFilter(StdOutFilter())

# Create a handler for stderr
stderr_handler = logging.StreamHandler(sys.stderr)
stderr_handler.setLevel(logging.WARNING) 
stderr_handler.setFormatter(formatter)

# Add the handlers to the logger
_root_logger.addHandler(stdout_handler)
_root_logger.addHandler(stderr_handler)