import argparse
import importlib.util
import logging
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).resolve().parents[1] / "data-parser" / "parser_niche_and_categories_v3.py"
SPEC = importlib.util.spec_from_file_location("parser_niche_and_categories_v3", MODULE_PATH)
parser = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
sys.modules[SPEC.name] = parser
SPEC.loader.exec_module(parser)


NORMAL_HTML = """
<html>
  <head><title>Real Publishing Site</title><meta name="description" content="Independent business coverage"></head>
  <body>
    <h1>Real Publishing Site</h1>
    <p>Independent business coverage, market analysis, founder interviews, and technology articles for operators.</p>
    <p>Readers use this publication for detailed practical guides, research, reviews, and company news.</p>
  </body>
</html>
"""

BOT_HTML = """
<html>
  <head><title>Just a moment...</title></head>
  <body><h1>Verifying you are a human</h1><p>Please wait while we verify your browser.</p></body>
</html>
"""


class ParserV3SeleniumHandlingTests(unittest.TestCase):
    def patch_attr(self, name, value):
        patcher = mock.patch.object(parser, name, value)
        self.addCleanup(patcher.stop)
        return patcher.start()

    def fetch(self):
        return parser.fetch_page(
            "https://example.com",
            request_timeout=1,
            selenium_timeout=3,
            selenium_hard_timeout=5,
            use_selenium_fallback=True,
            proxies=[],
            min_text_chars=40,
            selenium_headless=True,
            selenium_user_data_dir="",
        )

    def quality_for(self, page):
        extraction = parser.SiteExtraction(
            row_index=0,
            original_url="https://example.com",
            normalized_start_url="https://example.com",
            homepage=page,
        )
        return parser.evaluate_source_quality(extraction, min_text_chars=40)

    def test_http_normal_content_does_not_use_selenium(self):
        # Arrange
        selenium_called = False
        self.patch_attr("http_fetch", lambda *args, **kwargs: (NORMAL_HTML, 200, "https://example.com", ""))

        def selenium_fetch(*args, **kwargs):
            nonlocal selenium_called
            selenium_called = True
            return parser.SeleniumFetchResult(NORMAL_HTML, "https://example.com")

        self.patch_attr("selenium_fetch_with_hard_timeout", selenium_fetch)

        # Act
        result = self.fetch()

        # Assert
        self.assertEqual(result.page.fetch_method, "http")
        self.assertFalse(selenium_called)
        self.assertFalse(result.http_bot_protection_detected)

    def test_http_bot_protection_selenium_normal_content_is_not_protected(self):
        # Arrange
        self.patch_attr("http_fetch", lambda *args, **kwargs: (BOT_HTML, 200, "https://example.com", ""))
        self.patch_attr(
            "selenium_fetch_with_hard_timeout",
            lambda *args, **kwargs: parser.SeleniumFetchResult(NORMAL_HTML, "https://example.com/home"),
        )

        # Act
        result = self.fetch()

        # Assert
        self.assertEqual(result.page.fetch_method, "selenium")
        self.assertTrue(result.http_bot_protection_detected)
        self.assertTrue(result.bot_protection_solved_by_selenium)
        self.assertNotEqual(self.quality_for(result.page), "protected")

    def test_http_bot_protection_selenium_still_bot_protection_is_protected(self):
        # Arrange
        self.patch_attr("http_fetch", lambda *args, **kwargs: (BOT_HTML, 200, "https://example.com", ""))
        self.patch_attr(
            "selenium_fetch_with_hard_timeout",
            lambda *args, **kwargs: parser.SeleniumFetchResult(
                BOT_HTML,
                "https://example.com",
                bot_protection_detected=True,
                challenge_wait_attempted=True,
                challenge_attempts=2,
                challenge_wait_ms=3000,
            ),
        )

        # Act
        result = self.fetch()

        # Assert
        self.assertEqual(result.page.fetch_method, "selenium")
        self.assertTrue(result.http_bot_protection_detected)
        self.assertTrue(result.selenium_bot_protection_detected)
        self.assertEqual(self.quality_for(result.page), "protected")

    def test_http_failed_selenium_bot_protection_is_selenium_protected(self):
        # Arrange
        self.patch_attr("http_fetch", lambda *args, **kwargs: (None, None, "https://example.com", "http_failed"))
        self.patch_attr(
            "selenium_fetch_with_hard_timeout",
            lambda *args, **kwargs: parser.SeleniumFetchResult(
                BOT_HTML,
                "https://example.com",
                bot_protection_detected=True,
            ),
        )

        # Act
        result = self.fetch()

        # Assert
        self.assertEqual(result.page.fetch_method, "selenium")
        self.assertTrue(result.selenium_bot_protection_detected)
        self.assertEqual(self.quality_for(result.page), "protected")

    def install_fake_selenium_modules(self, page_sources):
        class FakeChromeOptions:
            def __init__(self):
                self.arguments = []
                self.page_load_strategy = ""

            def add_argument(self, value):
                self.arguments.append(value)

        class FakeDriver:
            def __init__(self, *args, **kwargs):
                self.sources = list(page_sources)
                self.current_url = "https://example.com"

            @property
            def page_source(self):
                if len(self.sources) > 1:
                    return self.sources.pop(0)
                return self.sources[0]

            def set_page_load_timeout(self, timeout):
                self.timeout = timeout

            def get(self, url):
                self.current_url = url

            def quit(self):
                return None

            def execute_script(self, script):
                return None

        uc = types.ModuleType("undetected_chromedriver")
        uc.ChromeOptions = FakeChromeOptions
        uc.Chrome = FakeDriver

        selenium = types.ModuleType("selenium")
        common = types.ModuleType("selenium.common")
        exceptions = types.ModuleType("selenium.common.exceptions")

        class TimeoutException(Exception):
            pass

        exceptions.TimeoutException = TimeoutException
        webdriver = types.ModuleType("selenium.webdriver")
        webdriver_common = types.ModuleType("selenium.webdriver.common")
        by = types.ModuleType("selenium.webdriver.common.by")
        by.By = types.SimpleNamespace(TAG_NAME="tag name")
        support = types.ModuleType("selenium.webdriver.support")
        expected_conditions = types.ModuleType("selenium.webdriver.support.expected_conditions")
        expected_conditions.presence_of_element_located = lambda locator: object()
        ui = types.ModuleType("selenium.webdriver.support.ui")

        class WebDriverWait:
            def __init__(self, driver, timeout):
                self.driver = driver
                self.timeout = timeout

            def until(self, condition):
                return True

        ui.WebDriverWait = WebDriverWait

        modules = {
            "undetected_chromedriver": uc,
            "selenium": selenium,
            "selenium.common": common,
            "selenium.common.exceptions": exceptions,
            "selenium.webdriver": webdriver,
            "selenium.webdriver.common": webdriver_common,
            "selenium.webdriver.common.by": by,
            "selenium.webdriver.support": support,
            "selenium.webdriver.support.expected_conditions": expected_conditions,
            "selenium.webdriver.support.ui": ui,
        }
        patcher = mock.patch.dict(sys.modules, modules)
        self.addCleanup(patcher.stop)
        patcher.start()

    def test_selenium_challenge_wait_returns_normal_content_when_challenge_clears(self):
        # Arrange
        self.install_fake_selenium_modules([BOT_HTML, NORMAL_HTML])
        now = {"value": 0.0}
        self.patch_attr("time", types.SimpleNamespace(
            monotonic=lambda: now["value"],
            sleep=lambda seconds: now.__setitem__("value", now["value"] + seconds),
        ))

        # Act
        result = parser.selenium_fetch_once("https://example.com", timeout=3, headless=True)

        # Assert
        self.assertIsNotNone(result.html)
        self.assertFalse(result.bot_protection_detected)
        self.assertTrue(result.challenge_wait_attempted)
        self.assertEqual(result.challenge_attempts, 1)

    def test_selenium_challenge_wait_remains_protected_after_retries(self):
        # Arrange
        self.install_fake_selenium_modules([BOT_HTML])
        now = {"value": 0.0}
        self.patch_attr("time", types.SimpleNamespace(
            monotonic=lambda: now["value"],
            sleep=lambda seconds: now.__setitem__("value", now["value"] + seconds),
        ))

        # Act
        result = parser.selenium_fetch_once("https://example.com", timeout=3, headless=True)

        # Assert
        self.assertIsNotNone(result.html)
        self.assertTrue(result.bot_protection_detected)
        self.assertTrue(result.challenge_wait_attempted)
        self.assertGreaterEqual(result.challenge_attempts, 2)

    def test_selenium_visible_disables_headless_arguments_and_driver_headless(self):
        # Arrange
        class FakeOptions:
            def __init__(self):
                self.arguments = []
                self.page_load_strategy = ""

            def add_argument(self, value):
                self.arguments.append(value)

        captured = {}

        class FakeUc:
            ChromeOptions = FakeOptions

            @staticmethod
            def Chrome(**kwargs):
                captured.update(kwargs)
                return kwargs

        # Act
        options = parser.build_chrome_options(FakeUc, headless=False)
        parser.create_uc_driver(FakeUc, headless=False)

        # Assert
        self.assertNotIn("--headless=new", options.arguments)
        self.assertFalse(captured["headless"])

    def test_selenium_user_data_dir_adds_argument_and_warns_for_concurrency(self):
        # Arrange
        class FakeOptions:
            def __init__(self):
                self.arguments = []
                self.page_load_strategy = ""

            def add_argument(self, value):
                self.arguments.append(value)

        class FakeUc:
            ChromeOptions = FakeOptions

        with tempfile.TemporaryDirectory() as temp_dir:
            profile_path = Path(temp_dir) / "profile"
            args = argparse.Namespace(selenium_user_data_dir=str(profile_path), concurrency=2)

            # Act
            options = parser.build_chrome_options(FakeUc, user_data_dir=str(profile_path))
            with self.assertLogs(level=logging.WARNING) as logs:
                parser.resolve_selenium_user_data_dir(args)

            # Assert
            self.assertIn(f"--user-data-dir={profile_path}", options.arguments)
            self.assertTrue(profile_path.exists())
            self.assertIn("one chrome profile should not be shared", "\n".join(logs.output).lower())

    def test_bot_protection_text_is_not_in_normal_claude_prompt_payload(self):
        # Arrange
        homepage, _links = parser.parse_html(
            BOT_HTML,
            url="https://example.com",
            fetch_method="selenium",
            http_status=200,
            final_url="https://example.com",
        )
        internal, _links = parser.parse_html(
            NORMAL_HTML,
            url="https://example.com/about",
            fetch_method="http",
            http_status=200,
            final_url="https://example.com/about",
        )
        extraction = parser.SiteExtraction(
            row_index=0,
            original_url="https://example.com",
            normalized_start_url="https://example.com",
            homepage=homepage,
            internal_pages=[internal],
            source_quality="limited",
        )

        # Act
        prompt = parser.build_user_prompt(
            extraction,
            max_prompt_chars=10000,
            min_categories=6,
            max_categories=12,
        )

        # Assert
        self.assertNotIn("Verifying you are a human", prompt)
        self.assertIn("Real Publishing Site", prompt)


if __name__ == "__main__":
    unittest.main()
