import json
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
with open("assets/config.json", 'r', encoding='utf-8') as f:
    _data = json.load(f)
cfg = dict_to_simplenamespace(_data)

import logging
logging.basicConfig(level=logging.DEBUG if cfg.DEBUG else logging.INFO,
                    format='{"level":"%(levelname)s", "data":%(message)s}')
logging.getLogger("PIL.PngImagePlugin").setLevel(logging.WARNING)

def _Log(log_func, message_dict, indent, **kwargs):
    if message_dict is None:
        message_dict = {}
    message_dict.update(kwargs)
    log_func(json.dumps(message_dict, indent=indent, ensure_ascii=False))

def LogDebug(message_dict=None, indent=None, **kwargs):
    if not cfg.DEBUG:
        return
    _Log(logging.debug, message_dict, indent, **kwargs)

def LogInfo(message_dict=None, indent=None, **kwargs):
    _Log(logging.info, message_dict, indent, **kwargs)

def LogWarning(message_dict=None, indent=None, **kwargs):
    _Log(logging.warning, message_dict, indent, **kwargs)

def LogError(message_dict=None, indent=None, **kwargs):
    _Log(logging.error, message_dict, indent, **kwargs)


def _override(func):
    """@override decorator - does nothing"""
    return func

if cfg.DEBUG:
    from overrides import override
else:
    override = _override