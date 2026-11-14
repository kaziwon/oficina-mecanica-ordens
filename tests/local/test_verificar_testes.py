import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
from verificar_testes import verificar


class VerificacaoTrxTests(unittest.TestCase):
    def criar(self, pasta, nome, total=2, passed=2, executed=2, not_executed=0):
        ns = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
        raiz = ET.Element(ns + "TestRun"); resumo = ET.SubElement(raiz, ns + "ResultSummary")
        ET.SubElement(resumo, ns + "Counters", total=str(total), passed=str(passed),
                      executed=str(executed), notExecuted=str(not_executed))
        arquivo = Path(pasta) / nome; ET.ElementTree(raiz).write(arquivo); return arquivo

    def test_duas_suites_completas_somam_todos_os_testes(self):
        with tempfile.TemporaryDirectory() as pasta:
            a = self.criar(pasta, "unit.trx"); b = self.criar(pasta, "integration.trx", 3, 3, 3)
            self.assertEqual(5, verificar([a, b]))

    def test_relatorio_ausente_ou_extra_falha(self):
        with tempfile.TemporaryDirectory() as pasta:
            a = self.criar(pasta, "a.trx")
            for arquivos in ([], [a], [a, a, a]):
                with self.assertRaises(ValueError): verificar(arquivos)

    def test_ignorados_e_falhas_nao_passam(self):
        with tempfile.TemporaryDirectory() as pasta:
            completo = self.criar(pasta, "a.trx")
            for total, passed, executed, ignorados in ((2, 1, 1, 1), (2, 1, 2, 0), (0, 0, 0, 0)):
                incompleto = self.criar(pasta, "b.trx", total, passed, executed, ignorados)
                with self.assertRaises(ValueError): verificar([completo, incompleto])

    def test_xml_sem_contadores_nao_passa(self):
        with tempfile.TemporaryDirectory() as pasta:
            a = self.criar(pasta, "a.trx"); b = Path(pasta) / "b.trx"
            ET.ElementTree(ET.Element("TestRun")).write(b)
            with self.assertRaises(ValueError): verificar([a, b])


if __name__ == "__main__":
    unittest.main()
