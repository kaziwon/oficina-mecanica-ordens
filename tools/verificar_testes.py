"""Confere que a suite executou todos os testes, sem ignorar integracoes."""

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

NAMESPACE = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def verificar(arquivos):
    if len(arquivos) != 2:
        raise ValueError("Esperados dois relatorios TRX: unidade e integracao.")
    total = 0
    for arquivo in arquivos:
        raiz = ET.parse(arquivo).getroot()
        contador = raiz.find("./t:ResultSummary/t:Counters", NAMESPACE)
        if contador is None:
            raise ValueError("Relatorio sem contadores de testes.")
        quantidade = int(contador.attrib["total"])
        if quantidade <= 0 or int(contador.attrib["passed"]) != quantidade \
                or int(contador.attrib["executed"]) != quantidade \
                or int(contador.attrib.get("notExecuted", "0")) != 0:
            raise ValueError("Ha testes com falha, ignorados ou sem execucao em " + arquivo.name)
        total += quantidade
    return total


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--diretorio", required=True, type=Path)
    args = parser.parse_args()
    try:
        total = verificar(sorted(args.diretorio.rglob("*.trx")))
        print(f"{total} testes passaram, sem ignorados.")
        return 0
    except (ValueError, OSError, KeyError, ET.ParseError) as erro:
        print("Falha ao verificar testes: " + str(erro), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
