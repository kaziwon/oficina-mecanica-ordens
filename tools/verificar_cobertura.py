"""Une cobertura por arquivo e linha, sem contar a mesma linha duas vezes."""

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

CAMADAS = ("Domain", "Application", "InterfaceAdapters", "Infrastructure", "Api")


def mesclar(relatorios, servico):
    prefixo = f"OficinaMecanica.{servico}."
    esperados = {prefixo + camada for camada in CAMADAS}
    linhas = {}
    encontrados = set()
    if not relatorios:
        raise ValueError("Nenhum relatorio Cobertura encontrado.")
    for arquivo in relatorios:
        raiz = ET.parse(arquivo).getroot()
        if raiz.tag != "coverage":
            raise ValueError("O arquivo nao e um relatorio Cobertura.")
        for pacote in raiz.findall("./packages/package"):
            nome = pacote.get("name")
            if nome not in esperados:
                continue
            encontrados.add(nome)
            for classe in pacote.findall("./classes/class"):
                caminho = (classe.get("filename") or "").replace("\\", "/")
                if not caminho:
                    raise ValueError("Classe sem arquivo de origem.")
                # O prefixo do projeto torna a chave independente da pasta do checkout.
                marcador = nome + "/"
                if marcador in caminho:
                    caminho = marcador + caminho.split(marcador, 1)[1]
                for linha in classe.findall("./lines/line"):
                    numero, hits = int(linha.attrib["number"]), int(linha.attrib["hits"])
                    if numero < 1 or hits < 0:
                        raise ValueError("Contagem de linhas invalida.")
                    chave = (nome, caminho, numero)
                    linhas[chave] = linhas.get(chave, False) or hits > 0
    ausentes = esperados - encontrados
    if ausentes:
        raise ValueError("Cobertura incompleta; faltam camadas: " + ", ".join(sorted(ausentes)))
    if not linhas:
        raise ValueError("Relatorios sem linhas executaveis.")
    por_camada = {}
    for nome in sorted(esperados):
        valores = [hit for (pacote, _, _), hit in linhas.items() if pacote == nome]
        if not valores:
            raise ValueError("Camada sem linhas executaveis: " + nome)
        por_camada[nome] = {"cobertas": sum(valores), "total": len(valores),
                            "percentual": round(100 * sum(valores) / len(valores), 2)}
    cobertas, total = sum(linhas.values()), len(linhas)
    return {"servico": servico, "cobertas": cobertas, "total": total, "percentual": 100 * cobertas / total,
            "camadas": por_camada}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--servico", choices=("Ordens", "Execucao", "Cobranca"), required=True)
    parser.add_argument("--diretorio", type=Path, required=True, help="Somente os resultados desta execucao de testes.")
    parser.add_argument("--minimo", type=float, default=80)
    parser.add_argument("--json", type=Path)
    args = parser.parse_args()
    if not 80 <= args.minimo <= 100:
        parser.error("O limite deve estar entre 80 e 100.")
    try:
        resultado = mesclar(sorted(args.diretorio.rglob("coverage.cobertura.xml")), args.servico)
        print(f"{args.servico}: {resultado['percentual']:.2f}% ({resultado['cobertas']}/{resultado['total']} linhas), minimo {args.minimo:g}%.")
        for nome, camada in resultado["camadas"].items():
            print(f"  {nome}: {camada['percentual']:.2f}%")
        if args.json:
            args.json.parent.mkdir(parents=True, exist_ok=True)
            args.json.write_text(json.dumps(resultado, indent=2) + "\n", encoding="utf-8")
        return 0 if resultado["percentual"] >= args.minimo else 1
    except (ValueError, KeyError, OSError, ET.ParseError) as erro:
        print("Falha ao verificar cobertura: " + str(erro), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
