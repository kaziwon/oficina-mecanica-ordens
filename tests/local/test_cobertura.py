import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
from verificar_cobertura import CAMADAS, mesclar


class CoberturaTests(unittest.TestCase):
    def relatorio(self, pasta, nome, hits, camadas=CAMADAS):
        raiz = ET.Element("coverage"); pacotes = ET.SubElement(raiz, "packages")
        for camada in camadas:
            pacote = ET.SubElement(pacotes, "package", name="OficinaMecanica.Ordens." + camada)
            classes = ET.SubElement(pacote, "classes")
            classe = ET.SubElement(classes, "class", filename=f"OficinaMecanica.Ordens.{camada}/Exemplo.cs")
            linhas = ET.SubElement(classe, "lines")
            for numero, hit in enumerate(hits, 1):
                ET.SubElement(linhas, "line", number=str(numero), hits=str(hit))
        caminho = Path(pasta) / nome; ET.ElementTree(raiz).write(caminho); return caminho

    def test_mescla_unidade_integracao_sem_duplicar_denominador(self):
        with tempfile.TemporaryDirectory() as pasta:
            a = self.relatorio(pasta, "unit.xml", [1, 0]); b = self.relatorio(pasta, "integration.xml", [0, 1])
            resultado = mesclar([a, b], "Ordens")
            self.assertEqual(10, resultado["total"]); self.assertEqual(100, resultado["percentual"])

    def test_nao_soma_percentuais_ou_esconde_camada_faltante(self):
        with tempfile.TemporaryDirectory() as pasta:
            a = self.relatorio(pasta, "a.xml", [1, 0]); b = self.relatorio(pasta, "b.xml", [1, 0])
            self.assertEqual(50, mesclar([a, b], "Ordens")["percentual"])
            incompleto = self.relatorio(pasta, "c.xml", [1], CAMADAS[:-1])
            with self.assertRaises(ValueError): mesclar([incompleto], "Ordens")

    def test_falha_sem_relatorio_ou_linhas(self):
        with self.assertRaises(ValueError): mesclar([], "Ordens")
        with tempfile.TemporaryDirectory() as pasta:
            vazio = self.relatorio(pasta, "vazio.xml", [])
            with self.assertRaises(ValueError): mesclar([vazio], "Ordens")


if __name__ == "__main__":
    unittest.main()
