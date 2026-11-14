#!/usr/bin/env python3
"""Serializa operacoes pesadas dos runners que compartilham o notebook."""

import subprocess
import sys
from local_runtime import operation_lock, runtime_home


def main():
    if len(sys.argv) < 2:
        raise ValueError("Informe o comando que sera executado com exclusao local.")
    with operation_lock(runtime_home()):
        return subprocess.run(sys.argv[1:], check=False).returncode


if __name__ == "__main__":
    sys.exit(main())
