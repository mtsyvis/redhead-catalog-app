#!/usr/bin/env python3
"""
parser_niche_and_categories_v2.py

Batch classifier for website Niche / Categories / Language.

Key design decisions:
- Uses Anthropic Message Batches API for cheaper bulk classification.
- Default model: claude-haiku-4-5. Optional: claude-sonnet-4-6.
- Keeps Niche as a clean controlled taxonomy.
- Keeps Categories as English search tags, including site-type / attribute-like tags when useful.
- Adds extraction/fetch/model analytics columns so bad classification can be traced back to weak input.
- Uses Selenium only as a fallback for homepage by default. Internal Selenium fallback is opt-in.

Install dependencies:
    pip install pandas openpyxl requests beautifulsoup4 tqdm anthropic undetected-chromedriver selenium

Environment:
    set ANTHROPIC_API_KEY=sk-ant-...     # Windows PowerShell: $env:ANTHROPIC_API_KEY="sk-ant-..."

Example test run:
    python parser_niche_and_categories_v2.py --input sites.xlsx --output sites_with_categories_v2.xlsx --max-sites 20

Try Sonnet for quality comparison:
    python parser_niche_and_categories_v2.py --input sites.xlsx --output sonnet_test.xlsx --max-sites 20 --model claude-sonnet-4-6

Extract only, no Claude batch:
    python parser_niche_and_categories_v2.py --input sites.xlsx --output extraction_debug.xlsx --max-sites 20 --extract-only
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import random
import re
import sys
import threading
import time
import traceback
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import asdict, dataclass, field
from html import unescape
from typing import Any, Dict, Iterable, List, Optional, Sequence, Tuple
from urllib.parse import urljoin, urlparse, urlunparse, urldefrag

import pandas as pd
import requests
from bs4 import BeautifulSoup
from tqdm import tqdm

# Set your Anthropic API key here or via the ANTHROPIC_API_KEY environment variable
ANTHROPIC_API_KEY = None

# ===================== TAXONOMY =====================
# Niche must answer: "What is this website mainly about?"
# Categories answer: "Which English search tags help a manager find this site?"
# Attribute-like terms such as SaaS, Blog, Marketplace, Directory, B2B, Tool, App are NOT niches;
# they are allowed in Categories when useful.
NICHES_LIST: List[str] = [
    "AI",
    "Architecture",
    "Art",
    "Alcohol",
    "Adult",
    "Automotive",
    "Beauty",
    "Betting",
    "Books",
    "Business",
    "Career",
    "Cleaning",
    "Comics",
    "Construction",
    "Crypto",
    "Cybersecurity",
    "Dating",
    "Death Care",
    "Economics",
    "Education",
    "Entertainment",
    "Events",
    "Expat",
    "Family",
    "Fashion",
    "Finance",
    "Fitness",
    "Food",
    "Furniture",
    "Gambling",
    "Gaming",
    "Gardening",
    "Government",
    "Health",
    "Home Improvement",
    "Immigration",
    "Insurance",
    "Interior Design",
    "Investment",
    "Law",
    "Lifestyle",
    "Literature",
    "Logistics",
    "Marketing",
    "Media",
    "Mental Health",
    "Movies",
    "Music",
    "News",
    "Parenting",
    "Pets",
    "Photography",
    "Politics",
    "Psychology",
    "Real Estate",
    "Religion",
    "Science",
    "Shopping",
    "Software",
    "Sports",
    "Sustainability",
    "Technology",
    "Telecom",
    "Transport",
    "Travel",
    "Wedding",
    "Wellness",
    "Writing",
]

SPECIAL_NICHES = ["UNKNOWN"]
ALLOWED_NICHES = set(NICHES_LIST + SPECIAL_NICHES)

# These are allowed in Categories, not in Niche. The model can use them if they help search/filtering.
CATEGORY_ATTRIBUTE_HINTS: List[str] = [
    "App",
    "B2B",
    "B2C",
    "Blog",
    "Booking platform",
    "Community",
    "CRM",
    "Directory",
    "Ecommerce",
    "Forum",
    "Job board",
    "Marketplace",
    "Media site",
    "Metasearch",
    "News site",
    "Platform",
    "Review site",
    "SaaS",
    "Search engine",
    "Service provider",
    "Social media",
    "Streaming",
    "Subscription box",
    "Templates",
    "Tool",
    "VPN",
    "Website builder",
]

# Very weak tags that should not be kept unless embedded in a meaningful phrase.
TOO_GENERIC_CATEGORY_TAGS = {
    "best",
    "top",
    "official",
    "website",
    "homepage",
    "online",
    "company",
    "services",
    "solutions",
    "blog",
    "news",
}

LANGUAGE_UNKNOWN = "UNKNOWN"
LANGUAGE_MULTI = "MULTI"

MODEL_CHOICES = ("claude-haiku-4-5", "claude-sonnet-4-6")
DEFAULT_MODEL = "claude-haiku-4-5"
DEFAULT_INPUT = "sites.xlsx"
DEFAULT_OUTPUT = "sites_with_categories_v2.xlsx"
DEFAULT_URL_COLUMN = None
DEFAULT_CHECKPOINT = "parser_v2_checkpoint.json"
DEFAULT_MAX_SITES = 20
DEFAULT_START_ROW = 0
DEFAULT_CONCURRENCY = 6
DEFAULT_REQUEST_TIMEOUT = 12
DEFAULT_INTERNAL_PAGES = 5
DEFAULT_MIN_TEXT_CHARS = 180
DEFAULT_MIN_INTERNAL_TEXT_CHARS = 120
DEFAULT_SELENIUM_TIMEOUT = 12
DEFAULT_SELENIUM_HARD_TIMEOUT = 25
DEFAULT_SELENIUM_INTERNAL_LIMIT = 2
DEFAULT_MAX_TOKENS = 700
DEFAULT_MAX_PROMPT_CHARS = 20_000
DEFAULT_POLL_INTERVAL = 30
DEFAULT_LOG_FILE = "parser_v2.log"
DEFAULT_PROXY: List[str] = []


# ===================== DATA STRUCTURES =====================
@dataclass
class PageExtract:
    url: str
    fetch_method: str
    http_status: Optional[int]
    final_url: str
    redirected: bool
    title: str = ""
    meta_description: str = ""
    meta_keywords: str = ""
    html_lang: str = ""
    h1: List[str] = field(default_factory=list)
    h2: List[str] = field(default_factory=list)
    nav_items: List[str] = field(default_factory=list)
    breadcrumbs: List[str] = field(default_factory=list)
    paragraphs: List[str] = field(default_factory=list)
    text_length: int = 0
    error: str = ""


@dataclass
class SiteExtraction:
    row_index: int
    original_url: str
    normalized_start_url: str
    homepage: Optional[PageExtract] = None
    internal_pages: List[PageExtract] = field(default_factory=list)
    internal_pages_requested: int = 0
    internal_pages_http_succeeded: int = 0
    internal_pages_selenium_tried: int = 0
    internal_pages_selenium_succeeded: int = 0
    processing_time_ms: int = 0
    source_quality: str = "failed"  # good / limited / poor / failed
    prompt_chars: int = 0
    estimated_input_tokens: int = 0
    exact_input_tokens: int = 0
    token_count_method: str = ""  # estimated / exact / skipped / failed
    error: str = ""

    @property
    def total_text_length(self) -> int:
        pages = ([self.homepage] if self.homepage else []) + self.internal_pages
        return sum(p.text_length for p in pages if p)

    @property
    def internal_pages_succeeded(self) -> int:
        return len([p for p in self.internal_pages if not p.error and p.text_length > 0])


@dataclass
class ClassificationResult:
    row_index: int
    original_url: str
    custom_id: str = ""
    niche: List[str] = field(default_factory=lambda: ["UNKNOWN"])
    categories: List[str] = field(default_factory=list)
    language: str = LANGUAGE_UNKNOWN
    model_name: str = ""
    input_tokens: int = 0
    output_tokens: int = 0
    estimated_cost_usd: float = 0.0
    batch_id: str = ""
    batch_status: str = ""
    model_error: str = ""
    raw_model_text: str = ""


# ===================== LOGGING / CHECKPOINT =====================
def setup_logging(log_file: str) -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(message)s",
        handlers=[logging.FileHandler(log_file, encoding="utf-8"), logging.StreamHandler(sys.stdout)],
    )


def load_checkpoint(path: str) -> Dict[str, Any]:
    if not path or not os.path.exists(path):
        return {}
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def save_checkpoint(path: str, payload: Dict[str, Any]) -> None:
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)
    os.replace(tmp, path)


# ===================== URL / HTTP HELPERS =====================
def normalize_input_url(value: Any) -> str:
    s = str(value or "").strip()
    if not s or s.lower() in {"nan", "none", "null"}:
        return ""
    s = s.replace(" ", "")
    if not re.match(r"^https?://", s, flags=re.I):
        # Prefer HTTPS first. HTTP is used as fallback if HTTPS fails.
        s = "https://" + s
    return s


def candidate_start_urls(value: Any) -> List[str]:
    normalized = normalize_input_url(value)
    if not normalized:
        return []
    parsed = urlparse(normalized)
    if parsed.scheme.lower() == "https":
        http_url = urlunparse(parsed._replace(scheme="http"))
        return [normalized, http_url]
    return [normalized]


def same_site(url_a: str, url_b: str) -> bool:
    host_a = (urlparse(url_a).netloc or "").lower().removeprefix("www.")
    host_b = (urlparse(url_b).netloc or "").lower().removeprefix("www.")
    return bool(host_a and host_a == host_b)


def clean_url(url: str) -> str:
    url, _frag = urldefrag(url)
    parsed = urlparse(url)
    # Remove common tracking query params by dropping query entirely for crawling stability.
    return urlunparse(parsed._replace(query=""))


def looks_like_html_url(url: str) -> bool:
    path = urlparse(url).path.lower()
    bad_ext = (
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp",
        ".svg",
        ".pdf",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
        ".zip",
        ".rar",
        ".mp4",
        ".mp3",
        ".avi",
        ".mov",
        ".css",
        ".js",
        ".xml",
        ".json",
    )
    return not path.endswith(bad_ext)


def is_bad_internal_link(url: str, anchor: str) -> bool:
    url_l = url.lower()
    anchor_l = (anchor or "").strip().lower()
    bad_parts = [
        "mailto:",
        "tel:",
        "javascript:",
        "/login",
        "/signin",
        "/sign-in",
        "/signup",
        "/register",
        "/cart",
        "/checkout",
        "/privacy",
        "/terms",
        "/cookie",
        "/contact",
        "/advertise",
        "/wp-login",
        "/author/",
        "/tag/",
        "?s=",
        "?search=",
        "/search",
        "/feed",
        "/rss",
    ]
    if any(part in url_l for part in bad_parts):
        return True
    if anchor_l in {"", "home", "homepage", "read more", "learn more", "click here", "more"}:
        return True
    return False


def http_fetch(url: str, timeout: int, proxies: Optional[Dict[str, str]] = None) -> Tuple[Optional[str], Optional[int], str, str]:
    headers = {
        "User-Agent": (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36"
        ),
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Accept-Language": "en-US,en;q=0.8",
    }
    try:
        resp = requests.get(url, headers=headers, timeout=timeout, proxies=proxies, allow_redirects=True)
        ctype = resp.headers.get("content-type", "").lower()
        text = resp.text or ""
        if resp.status_code >= 400:
            return None, resp.status_code, resp.url, f"http_status_{resp.status_code}"
        if "text/html" not in ctype and "application/xhtml" not in ctype and len(text) < 500:
            return None, resp.status_code, resp.url, f"non_html_content_type:{ctype}"
        if len(text) < 200:
            return None, resp.status_code, resp.url, "too_short_html"
        return text, resp.status_code, resp.url, ""
    except Exception as e:
        return None, None, url, f"http_exception:{type(e).__name__}:{e}"


# ===================== SELENIUM FALLBACK =====================
def selenium_fetch_once(url: str, timeout: int, proxy: Optional[str] = None) -> Tuple[Optional[str], str, str]:
    try:
        import undetected_chromedriver as uc
        from selenium.webdriver.common.by import By
        from selenium.webdriver.support import expected_conditions as EC
        from selenium.webdriver.support.ui import WebDriverWait
    except Exception as e:
        return None, url, f"selenium_import_failed:{type(e).__name__}:{e}"

    driver = None
    try:
        options = uc.ChromeOptions()
        options.add_argument("--headless=new")
        options.add_argument("--no-sandbox")
        options.add_argument("--disable-dev-shm-usage")
        options.add_argument("--disable-gpu")
        options.add_argument("--disable-blink-features=AutomationControlled")
        options.add_argument("--no-first-run")
        options.add_argument("--no-default-browser-check")
        if proxy:
            options.add_argument(f"--proxy-server={proxy}")

        driver = uc.Chrome(options=options, headless=True, use_subprocess=True)
        driver.set_page_load_timeout(timeout)
        driver.get(url)
        WebDriverWait(driver, timeout).until(EC.presence_of_element_located((By.TAG_NAME, "body")))
        time.sleep(0.4)
        return driver.page_source, driver.current_url, ""
    except Exception as e:
        return None, url, f"selenium_exception:{type(e).__name__}:{e}"
    finally:
        if driver is not None:
            try:
                driver.quit()
            except Exception:
                pass


def selenium_fetch_with_hard_timeout(
    url: str,
    timeout: int,
    hard_timeout: int,
    proxy: Optional[str] = None,
) -> Tuple[Optional[str], str, str]:
    result: Dict[str, Any] = {"html": None, "final_url": url, "error": ""}

    def run() -> None:
        html, final_url, err = selenium_fetch_once(url, timeout=timeout, proxy=proxy)
        result["html"] = html
        result["final_url"] = final_url
        result["error"] = err

    thread = threading.Thread(target=run, daemon=True)
    thread.start()
    thread.join(timeout=hard_timeout)
    if thread.is_alive():
        return None, url, f"selenium_hard_timeout_{hard_timeout}s"
    return result["html"], result["final_url"], result["error"]


# ===================== HTML EXTRACTION =====================
def compact_text(text: str, max_len: int = 500) -> str:
    if not text:
        return ""
    text = unescape(text)
    text = re.sub(r"\s+", " ", text).strip()
    if len(text) > max_len:
        text = text[: max_len - 1].rstrip() + "…"
    return text


def unique_keep_order(items: Iterable[str], limit: int) -> List[str]:
    seen = set()
    result = []
    for item in items:
        cleaned = compact_text(item)
        key = cleaned.lower()
        if not cleaned or key in seen:
            continue
        seen.add(key)
        result.append(cleaned)
        if len(result) >= limit:
            break
    return result


def extract_links(soup: BeautifulSoup, base_url: str, limit: int) -> List[str]:
    if limit <= 0:
        return []

    candidates: List[Tuple[int, str, str]] = []

    def add_link(a: Any, context_score: int) -> None:
        href = a.get("href") if a else None
        if not href:
            return
        anchor = compact_text(a.get_text(" ", strip=True), 120)
        full = clean_url(urljoin(base_url, href))
        if not looks_like_html_url(full):
            return
        if not same_site(base_url, full):
            return
        if clean_url(full).rstrip("/") == clean_url(base_url).rstrip("/"):
            return
        if is_bad_internal_link(full, anchor):
            return
        url_l = full.lower()
        anchor_l = anchor.lower()
        score = context_score
        strong_terms = [
            "category",
            "categories",
            "topic",
            "topics",
            "section",
            "sections",
            "blog",
            "article",
            "articles",
            "resources",
            "services",
            "industries",
            "solutions",
            "about",
            "guide",
            "guides",
        ]
        if any(term in url_l or term in anchor_l for term in strong_terms):
            score += 20
        # Prefer meaningful anchor text over icon/header links.
        if len(anchor_l) >= 4:
            score += min(len(anchor_l), 40) // 4
        candidates.append((score, full, anchor))

    for nav in soup.find_all(["nav", "header"]):
        for a in nav.find_all("a", href=True):
            add_link(a, 30)

    for menu in soup.find_all(["ul", "ol", "div"]):
        classes = " ".join(menu.get("class") or []).lower()
        ident = (menu.get("id") or "").lower()
        if any(k in classes for k in ["menu", "nav", "category", "topic", "section"]) or any(
            k in ident for k in ["menu", "nav", "category", "topic", "section"]
        ):
            for a in menu.find_all("a", href=True):
                add_link(a, 20)

    for a in soup.find_all("a", href=True):
        add_link(a, 0)

    # Highest score first, unique URLs.
    candidates.sort(key=lambda x: x[0], reverse=True)
    result = []
    seen = set()
    for _score, url, _anchor in candidates:
        key = url.rstrip("/")
        if key in seen:
            continue
        seen.add(key)
        result.append(url)
        if len(result) >= limit:
            break
    return result


def parse_html(html: str, url: str, fetch_method: str, http_status: Optional[int], final_url: str) -> Tuple[PageExtract, List[str]]:
    soup = BeautifulSoup(html or "", "html.parser")

    for tag in soup(["script", "style", "noscript", "svg", "canvas", "iframe"]):
        tag.decompose()

    title = compact_text(soup.title.string if soup.title and soup.title.string else "", 300)

    meta_desc = ""
    meta_desc_tag = soup.find("meta", attrs={"name": re.compile(r"^description$", re.I)})
    if meta_desc_tag and meta_desc_tag.get("content"):
        meta_desc = compact_text(meta_desc_tag.get("content", ""), 500)

    meta_keywords = ""
    meta_keywords_tag = soup.find("meta", attrs={"name": re.compile(r"^keywords$", re.I)})
    if meta_keywords_tag and meta_keywords_tag.get("content"):
        meta_keywords = compact_text(meta_keywords_tag.get("content", ""), 500)

    html_lang = ""
    try:
        if soup.html and soup.html.has_attr("lang"):
            html_lang = compact_text(str(soup.html.get("lang") or ""), 40)
    except Exception:
        html_lang = ""

    h1 = unique_keep_order((h.get_text(" ", strip=True) for h in soup.find_all("h1")), limit=8)
    h2 = unique_keep_order((h.get_text(" ", strip=True) for h in soup.find_all("h2")), limit=16)

    nav_items: List[str] = []
    for nav in soup.find_all("nav"):
        nav_items.extend([a.get_text(" ", strip=True) for a in nav.find_all("a")])
        nav_items.extend([li.get_text(" ", strip=True) for li in nav.find_all("li")])
    for menu in soup.find_all(["ul", "ol", "div"]):
        classes = " ".join(menu.get("class") or []).lower()
        ident = (menu.get("id") or "").lower()
        if any(k in classes for k in ["menu", "nav", "category", "topic"]) or any(
            k in ident for k in ["menu", "nav", "category", "topic"]
        ):
            nav_items.extend([li.get_text(" ", strip=True) for li in menu.find_all("li")])
            nav_items.extend([a.get_text(" ", strip=True) for a in menu.find_all("a")])
    nav_items = unique_keep_order(nav_items, limit=40)

    breadcrumbs: List[str] = []
    for node in soup.find_all(class_=re.compile(r"breadcrumb", re.I)):
        breadcrumbs.append(node.get_text(" ", strip=True))
    breadcrumbs = unique_keep_order(breadcrumbs, limit=10)

    paragraphs = unique_keep_order((p.get_text(" ", strip=True) for p in soup.find_all("p")), limit=18)

    text_parts = [title, meta_desc, meta_keywords] + h1 + h2 + nav_items + breadcrumbs + paragraphs
    text_length = len(" ".join([p for p in text_parts if p]))

    page = PageExtract(
        url=url,
        fetch_method=fetch_method,
        http_status=http_status,
        final_url=final_url,
        redirected=clean_url(url).rstrip("/") != clean_url(final_url).rstrip("/"),
        title=title,
        meta_description=meta_desc,
        meta_keywords=meta_keywords,
        html_lang=html_lang,
        h1=h1,
        h2=h2,
        nav_items=nav_items,
        breadcrumbs=breadcrumbs,
        paragraphs=paragraphs,
        text_length=text_length,
    )

    links = extract_links(soup, final_url or url, limit=50)
    return page, links


def page_to_prompt_payload(page: PageExtract) -> Dict[str, Any]:
    return {
        "url": page.final_url or page.url,
        "fetch_method": page.fetch_method,
        "title": page.title,
        "meta_description": page.meta_description,
        "meta_keywords": page.meta_keywords,
        "html_lang": page.html_lang,
        "h1": page.h1,
        "h2": page.h2,
        "nav_items": page.nav_items,
        "breadcrumbs": page.breadcrumbs,
        "paragraphs": page.paragraphs,
    }


# ===================== SITE CRAWLING =====================
def choose_proxy(proxies: Sequence[str]) -> Optional[str]:
    return random.choice(list(proxies)) if proxies else None


def fetch_page(
    url: str,
    *,
    request_timeout: int,
    selenium_timeout: int,
    selenium_hard_timeout: int,
    use_selenium_fallback: bool,
    proxies: Sequence[str],
    min_text_chars: int,
) -> Tuple[Optional[PageExtract], List[str], str]:
    proxy = choose_proxy(proxies)
    proxy_dict = {"http": proxy, "https": proxy} if proxy else None

    html, status, final_url, err = http_fetch(url, timeout=request_timeout, proxies=proxy_dict)
    if html:
        page, links = parse_html(html, url=url, fetch_method="http", http_status=status, final_url=final_url)
        if page.text_length >= min_text_chars or not use_selenium_fallback:
            return page, links, ""
        # Text exists but is too weak. Let Selenium try to render it.
        err = f"weak_http_text:{page.text_length}"

    if not use_selenium_fallback:
        return None, [], err or "http_failed"

    selenium_proxy = choose_proxy(proxies)
    html2, final_url2, selenium_err = selenium_fetch_with_hard_timeout(
        url,
        timeout=selenium_timeout,
        hard_timeout=selenium_hard_timeout,
        proxy=selenium_proxy,
    )
    if html2:
        page2, links2 = parse_html(html2, url=url, fetch_method="selenium", http_status=status, final_url=final_url2)
        if page2.text_length > 0:
            return page2, links2, ""
    return None, [], selenium_err or err or "fetch_failed"


def evaluate_source_quality(extraction: SiteExtraction, min_text_chars: int) -> str:
    if extraction.error and not extraction.homepage:
        return "failed"
    total = extraction.total_text_length
    if total >= max(1500, min_text_chars * 4) and extraction.internal_pages_succeeded >= 2:
        return "good"
    if total >= max(700, min_text_chars * 2):
        return "limited"
    if total >= min_text_chars:
        return "poor"
    return "failed"


def crawl_site(row_index: int, original_url: str, args: argparse.Namespace) -> SiteExtraction:
    start_time = time.time()
    extraction = SiteExtraction(
        row_index=row_index,
        original_url=str(original_url or "").strip(),
        normalized_start_url=normalize_input_url(original_url),
        internal_pages_requested=max(0, args.internal_pages),
    )

    candidates = candidate_start_urls(original_url)
    if not candidates:
        extraction.error = "empty_url"
        extraction.processing_time_ms = int((time.time() - start_time) * 1000)
        return extraction

    homepage = None
    homepage_links: List[str] = []
    last_error = ""
    for candidate in candidates:
        page, links, err = fetch_page(
            candidate,
            request_timeout=args.request_timeout,
            selenium_timeout=args.selenium_timeout,
            selenium_hard_timeout=args.selenium_hard_timeout,
            use_selenium_fallback=not args.no_selenium,
            proxies=args.proxies,
            min_text_chars=args.min_text_chars,
        )
        if page:
            homepage = page
            homepage_links = links
            break
        last_error = err

    if not homepage:
        extraction.error = last_error or "homepage_fetch_failed"
        extraction.processing_time_ms = int((time.time() - start_time) * 1000)
        extraction.source_quality = evaluate_source_quality(extraction, args.min_text_chars)
        return extraction

    extraction.homepage = homepage

    selected_links = homepage_links[: max(0, args.internal_pages)]
    selenium_internal_remaining = max(0, args.selenium_internal_limit if args.selenium_internal_fallback else 0)

    for link in selected_links:
        if len(extraction.internal_pages) >= args.internal_pages:
            break
        proxy = choose_proxy(args.proxies)
        proxy_dict = {"http": proxy, "https": proxy} if proxy else None
        html, status, final_url, err = http_fetch(link, timeout=args.request_timeout, proxies=proxy_dict)
        if html:
            page, _links = parse_html(html, url=link, fetch_method="http", http_status=status, final_url=final_url)
            if page.text_length >= args.min_internal_text_chars:
                extraction.internal_pages.append(page)
                extraction.internal_pages_http_succeeded += 1
                continue

        if args.selenium_internal_fallback and selenium_internal_remaining > 0 and not args.no_selenium:
            extraction.internal_pages_selenium_tried += 1
            selenium_internal_remaining -= 1
            html2, final_url2, selenium_err = selenium_fetch_with_hard_timeout(
                link,
                timeout=args.selenium_timeout,
                hard_timeout=args.selenium_hard_timeout,
                proxy=choose_proxy(args.proxies),
            )
            if html2:
                page2, _links2 = parse_html(html2, url=link, fetch_method="selenium", http_status=status, final_url=final_url2)
                if page2.text_length >= args.min_internal_text_chars:
                    extraction.internal_pages.append(page2)
                    extraction.internal_pages_selenium_succeeded += 1
                    continue
            logging.debug("Internal Selenium failed for %s: %s", link, selenium_err)
        else:
            logging.debug("Internal HTTP failed/weak for %s: %s", link, err)

    extraction.processing_time_ms = int((time.time() - start_time) * 1000)
    extraction.source_quality = evaluate_source_quality(extraction, args.min_text_chars)
    return extraction


# ===================== CLAUDE PROMPT / VALIDATION =====================
def build_system_prompt() -> str:
    return (
        "You classify websites for an advertising placement catalog. "
        "Return only valid JSON. Do not include explanations. "
        "Use English for Niche and Categories even if the website content is not English. "
        "Language must be the primary content language as an ISO 639-1 uppercase code like EN, DE, FR, ES, ID, RU; "
        "use MULTI for genuinely multilingual sites and UNKNOWN if unclear."
    )


def build_user_prompt(extraction: SiteExtraction, max_prompt_chars: int) -> str:
    payload = {
        "original_url": extraction.original_url,
        "source_quality": extraction.source_quality,
        "niche_whitelist": NICHES_LIST,
        "special_niches": SPECIAL_NICHES,
        "category_attribute_hints": CATEGORY_ATTRIBUTE_HINTS,
        "rules": [
            "Niche must be 1 to 3 values chosen only from niche_whitelist or special_niches.",
            "Use UNKNOWN only when there is not enough real content to classify the website.",
            "Use Business or Lifestyle only when no more specific niche clearly applies.",
            "Categories must be 7 to 14 English search tags when there is enough content.",
            "Categories may include sub-niches, synonyms, products, services, audience terms, and site-type/attribute terms such as SaaS, Blog, Marketplace, Directory, Tool, App, B2B, B2C.",
            "Do not put language codes or language names inside Categories.",
            "Avoid generic tags like online, website, homepage, company, best, top, official unless part of a specific meaningful phrase.",
            "Base the answer only on the provided extracted website data. Do not guess from the domain alone unless the extracted data is also consistent.",
        ],
        "required_json_shape": {
            "Niche": ["Finance", "Software"],
            "Categories": ["B2B SaaS", "accounting software", "invoice management"],
            "Language": "EN",
        },
        "extracted_pages": [],
    }
    if extraction.homepage:
        payload["extracted_pages"].append({"type": "homepage", **page_to_prompt_payload(extraction.homepage)})
    for page in extraction.internal_pages:
        payload["extracted_pages"].append({"type": "internal", **page_to_prompt_payload(page)})

    # Keep cost predictable. We include pages in priority order: homepage first, then selected internals.
    # If the prompt is too large, drop internal pages from the end before submitting.
    rendered = json.dumps(payload, ensure_ascii=False, indent=2)
    if max_prompt_chars and len(rendered) > max_prompt_chars and len(payload["extracted_pages"]) > 1:
        while len(payload["extracted_pages"]) > 1:
            payload["extracted_pages"].pop()
            rendered = json.dumps(payload, ensure_ascii=False, indent=2)
            if len(rendered) <= max_prompt_chars:
                break

    # Last-resort trim for pathological homepages. Do not trim metadata/rules; only reduce text-heavy lists.
    if max_prompt_chars and len(rendered) > max_prompt_chars and payload["extracted_pages"]:
        page = payload["extracted_pages"][0]
        page["paragraphs"] = page.get("paragraphs", [])[:6]
        page["nav_items"] = page.get("nav_items", [])[:25]
        page["h2"] = page.get("h2", [])[:8]
        rendered = json.dumps(payload, ensure_ascii=False, indent=2)

    return rendered


def safe_load_json_object(text: str) -> Optional[Dict[str, Any]]:
    if not text:
        return None
    cleaned = text.strip()
    cleaned = re.sub(r"^```(?:json)?\s*", "", cleaned, flags=re.I)
    cleaned = re.sub(r"\s*```$", "", cleaned)
    try:
        obj = json.loads(cleaned)
        return obj if isinstance(obj, dict) else None
    except Exception:
        pass

    # Last resort: extract first JSON object.
    match = re.search(r"\{.*\}", cleaned, flags=re.S)
    if match:
        try:
            obj = json.loads(match.group(0))
            return obj if isinstance(obj, dict) else None
        except Exception:
            return None
    return None


def coerce_list(value: Any) -> List[str]:
    if value is None:
        return []
    if isinstance(value, list):
        raw = value
    elif isinstance(value, str):
        raw = re.split(r"[,;|]", value)
    else:
        raw = [str(value)]
    return [compact_text(str(x), 120) for x in raw if compact_text(str(x), 120)]


def normalize_niches(value: Any) -> List[str]:
    raw = coerce_list(value)
    result: List[str] = []
    # Case-insensitive mapping back to canonical whitelist value.
    canonical = {n.lower(): n for n in ALLOWED_NICHES}
    for item in raw:
        key = item.strip().lower()
        if key in canonical and canonical[key] not in result:
            result.append(canonical[key])
    result = [n for n in result if n != "UNKNOWN"][:3]
    return result or ["UNKNOWN"]


def normalize_categories(value: Any, niches: List[str]) -> List[str]:
    if niches == ["UNKNOWN"]:
        return []
    raw = coerce_list(value)
    result: List[str] = []
    seen = set()
    for item in raw:
        cleaned = compact_text(item, 100).strip(" ,.;:-")
        if not cleaned:
            continue
        if re.fullmatch(r"[A-Z]{2}", cleaned.upper()):
            continue
        if cleaned.lower() in {"english", "german", "french", "spanish", "russian", "indonesian", "multi", "multilingual"}:
            continue
        if cleaned.lower() in TOO_GENERIC_CATEGORY_TAGS:
            continue
        key = cleaned.lower()
        if key in seen:
            continue
        seen.add(key)
        result.append(cleaned)
        if len(result) >= 14:
            break
    return result


def normalize_language(value: Any) -> str:
    s = compact_text(str(value or ""), 40).upper().replace("-", "_")
    if not s or s in {"N/A", "NA", "NONE", "NULL", "UNKNOWN"}:
        return LANGUAGE_UNKNOWN
    if s in {"MULTI", "MULTILINGUAL", "MIXED"}:
        return LANGUAGE_MULTI
    # Convert EN_US / PT_BR to EN / PT.
    if re.fullmatch(r"[A-Z]{2}_[A-Z]{2}", s):
        return s[:2]
    if re.fullmatch(r"[A-Z]{2}", s):
        return s
    # Basic language names fallback.
    name_map = {
        "ENGLISH": "EN",
        "GERMAN": "DE",
        "FRENCH": "FR",
        "SPANISH": "ES",
        "ITALIAN": "IT",
        "PORTUGUESE": "PT",
        "POLISH": "PL",
        "RUSSIAN": "RU",
        "UKRAINIAN": "UK",
        "INDONESIAN": "ID",
        "DUTCH": "NL",
        "TURKISH": "TR",
    }
    return name_map.get(s, LANGUAGE_UNKNOWN)


def parse_and_validate_model_result(
    row_index: int,
    original_url: str,
    custom_id: str,
    model_name: str,
    batch_id: str,
    batch_status: str,
    raw_text: str,
) -> ClassificationResult:
    result = ClassificationResult(
        row_index=row_index,
        original_url=original_url,
        custom_id=custom_id,
        model_name=model_name,
        batch_id=batch_id,
        batch_status=batch_status,
        raw_model_text=raw_text or "",
    )
    obj = safe_load_json_object(raw_text)
    if not obj:
        result.niche = ["UNKNOWN"]
        result.categories = []
        result.language = LANGUAGE_UNKNOWN
        result.model_error = "model_returned_invalid_json"
        return result

    result.niche = normalize_niches(obj.get("Niche") or obj.get("niche") or obj.get("niches"))
    result.categories = normalize_categories(obj.get("Categories") or obj.get("categories") or obj.get("Category"), result.niche)
    result.language = normalize_language(obj.get("Language") or obj.get("language"))
    return result


# ===================== ANTHROPIC BATCH =====================
def resolve_anthropic_api_key(cli_api_key: Optional[str] = None) -> str:
    for candidate in [cli_api_key, ANTHROPIC_API_KEY, os.getenv("ANTHROPIC_API_KEY")]:
        key = (candidate or "").strip()
        if key:
            return key
    raise RuntimeError(
        "Anthropic API key is missing. Provide it via --api-key, "
        "ANTHROPIC_API_KEY constant, or ANTHROPIC_API_KEY env var."
    )


def create_anthropic_client(api_key: Optional[str] = None) -> Any:
    try:
        import anthropic
    except Exception as e:
        raise RuntimeError(
            "Anthropic SDK is not installed. Run: pip install anthropic"
        ) from e
    return anthropic.Anthropic(api_key=resolve_anthropic_api_key(api_key))


def estimate_tokens_from_text(text: str) -> int:
    # Conservative rough estimate for English-ish JSON prompts. Exact counting is available via --count-tokens.
    return max(1, int(len(text) / 4) + 1)


def calculate_batch_cost_usd(model: str, input_tokens: int, output_tokens: int) -> float:
    prices = BATCH_PRICING_USD_PER_MTOK.get(model, BATCH_PRICING_USD_PER_MTOK[DEFAULT_MODEL])
    return round((input_tokens / 1_000_000) * prices["input"] + (output_tokens / 1_000_000) * prices["output"], 8)


def count_input_tokens_exact(client: Any, model: str, system_prompt: str, user_prompt: str) -> Tuple[int, str]:
    try:
        response = client.messages.count_tokens(
            model=model,
            system=system_prompt,
            messages=[{"role": "user", "content": user_prompt}],
        )
        return int(getattr(response, "input_tokens", 0) or 0), "exact"
    except Exception as e:
        logging.warning("Token counting failed: %s", e)
        return 0, "failed"


def create_batch_requests(extractions: List[SiteExtraction], model: str, max_tokens: int, max_prompt_chars: int, client: Optional[Any] = None, count_tokens: bool = False) -> Tuple[List[Any], Dict[str, SiteExtraction]]:
    try:
        from anthropic.types.message_create_params import MessageCreateParamsNonStreaming
        from anthropic.types.messages.batch_create_params import Request
    except Exception as e:
        raise RuntimeError(
            "Anthropic SDK batch types are unavailable. Upgrade SDK: pip install --upgrade anthropic"
        ) from e

    requests_payload = []
    mapping: Dict[str, SiteExtraction] = {}
    system_prompt = build_system_prompt()

    for seq, extraction in enumerate(extractions, start=1):
        if extraction.source_quality == "failed" or not extraction.homepage:
            continue
        custom_id = f"site_{seq:06d}"
        mapping[custom_id] = extraction
        user_prompt = build_user_prompt(extraction, max_prompt_chars=max_prompt_chars)
        prompt_chars = len(system_prompt) + len(user_prompt)
        extraction.prompt_chars = prompt_chars
        extraction.estimated_input_tokens = estimate_tokens_from_text(system_prompt + "\n" + user_prompt)
        extraction.token_count_method = "estimated"
        if count_tokens and client is not None:
            exact_tokens, method = count_input_tokens_exact(client, model, system_prompt, user_prompt)
            if exact_tokens > 0:
                extraction.exact_input_tokens = exact_tokens
                extraction.token_count_method = method
            else:
                extraction.token_count_method = method
        requests_payload.append(
            Request(
                custom_id=custom_id,
                params=MessageCreateParamsNonStreaming(
                    model=model,
                    max_tokens=max_tokens,
                    temperature=0,
                    system=system_prompt,
                    messages=[{"role": "user", "content": user_prompt}],
                ),
            )
        )
    return requests_payload, mapping


def submit_batch(client: Any, requests_payload: List[Any]) -> Any:
    if not requests_payload:
        return None
    return client.messages.batches.create(requests=requests_payload)


def poll_batch(client: Any, batch_id: str, poll_interval: int) -> Any:
    while True:
        batch = client.messages.batches.retrieve(batch_id)
        logging.info("Batch %s status=%s counts=%s", batch_id, batch.processing_status, getattr(batch, "request_counts", None))
        if batch.processing_status == "ended":
            return batch
        time.sleep(poll_interval)


def extract_text_from_message(message: Any) -> str:
    parts: List[str] = []
    for block in getattr(message, "content", []) or []:
        if getattr(block, "type", None) == "text":
            parts.append(getattr(block, "text", ""))
        elif isinstance(block, dict) and block.get("type") == "text":
            parts.append(block.get("text", ""))
    return "\n".join(parts).strip()


def read_batch_results(
    client: Any,
    batch_id: str,
    batch_status: str,
    custom_id_map: Dict[str, SiteExtraction],
    model_name: str,
) -> Dict[int, ClassificationResult]:
    by_row: Dict[int, ClassificationResult] = {}
    for item in client.messages.batches.results(batch_id):
        custom_id = item.custom_id
        extraction = custom_id_map.get(custom_id)
        if not extraction:
            logging.warning("Result custom_id not found in mapping: %s", custom_id)
            continue

        result_type = getattr(item.result, "type", "")
        if result_type == "succeeded":
            message = item.result.message
            raw_text = extract_text_from_message(message)
            parsed = parse_and_validate_model_result(
                row_index=extraction.row_index,
                original_url=extraction.original_url,
                custom_id=custom_id,
                model_name=model_name,
                batch_id=batch_id,
                batch_status=batch_status,
                raw_text=raw_text,
            )
            usage = getattr(message, "usage", None)
            parsed.input_tokens = int(getattr(usage, "input_tokens", 0) or 0)
            parsed.output_tokens = int(getattr(usage, "output_tokens", 0) or 0)
            parsed.estimated_cost_usd = calculate_batch_cost_usd(model_name, parsed.input_tokens, parsed.output_tokens)
            by_row[extraction.row_index] = parsed
        else:
            err = ""
            try:
                err = json.dumps(item.result.model_dump(), ensure_ascii=False)
            except Exception:
                err = str(item.result)
            by_row[extraction.row_index] = ClassificationResult(
                row_index=extraction.row_index,
                original_url=extraction.original_url,
                custom_id=custom_id,
                niche=["UNKNOWN"],
                categories=[],
                language=LANGUAGE_UNKNOWN,
                model_name=model_name,
                batch_id=batch_id,
                batch_status=batch_status,
                model_error=f"batch_result_{result_type}:{err[:500]}",
            )
    return by_row


# ===================== EXCEL OUTPUT =====================
def detect_url_column(df: pd.DataFrame, explicit: Optional[str]) -> str:
    if explicit:
        if explicit not in df.columns:
            raise RuntimeError(f"URL column '{explicit}' not found. Available columns: {list(df.columns)}")
        return explicit
    for col in ["Website", "URL", "Url", "Domain", "domain"]:
        if col in df.columns:
            return col
    raise RuntimeError("Input file must contain one of these columns: Website, URL, Url, Domain")


def empty_classification_for_extraction(extraction: SiteExtraction, model_name: str, batch_id: str, batch_status: str) -> ClassificationResult:
    return ClassificationResult(
        row_index=extraction.row_index,
        original_url=extraction.original_url,
        niche=["UNKNOWN"],
        categories=[],
        language=LANGUAGE_UNKNOWN,
        model_name=model_name,
        batch_id=batch_id,
        batch_status=batch_status,
        model_error="not_sent_to_model_due_to_failed_extraction" if extraction.source_quality == "failed" else "",
    )


def apply_results_to_dataframe(
    df: pd.DataFrame,
    extractions: List[SiteExtraction],
    classifications_by_row: Dict[int, ClassificationResult],
    model_name: str,
    batch_id: str,
    batch_status: str,
) -> pd.DataFrame:
    output = df.copy()
    output_columns = [
        "Niche",
        "Categories",
        "Language",
        "Error",
        "HomepageFetchMethod",
        "FinalUrl",
        "Redirected",
        "HttpStatus",
        "HomepageTextLength",
        "InternalPagesRequested",
        "InternalPagesHttpSucceeded",
        "InternalPagesSeleniumTried",
        "InternalPagesSeleniumSucceeded",
        "InternalPagesSucceeded",
        "TotalExtractedTextLength",
        "SourceQuality",
        "PromptChars",
        "EstimatedInputTokens",
        "ExactInputTokens",
        "ActualInputTokens",
        "OutputTokens",
        "EstimatedCostUsd",
        "TokenCountMethod",
        "ModelName",
        "BatchId",
        "BatchStatus",
        "ModelError",
        "ProcessingTimeMs",
    ]
    for col in output_columns:
        if col not in output.columns:
            output[col] = ""

    for extraction in extractions:
        row = extraction.row_index
        classification = classifications_by_row.get(row) or empty_classification_for_extraction(
            extraction, model_name=model_name, batch_id=batch_id, batch_status=batch_status
        )
        homepage = extraction.homepage
        errors = [e for e in [extraction.error, classification.model_error] if e]

        output.at[row, "Niche"] = ", ".join(classification.niche or ["UNKNOWN"])
        output.at[row, "Categories"] = ", ".join(classification.categories or [])
        output.at[row, "Language"] = classification.language or LANGUAGE_UNKNOWN
        output.at[row, "Error"] = "; ".join(errors)
        output.at[row, "HomepageFetchMethod"] = homepage.fetch_method if homepage else "failed"
        output.at[row, "FinalUrl"] = homepage.final_url if homepage else ""
        output.at[row, "Redirected"] = homepage.redirected if homepage else ""
        output.at[row, "HttpStatus"] = homepage.http_status if homepage and homepage.http_status is not None else ""
        output.at[row, "HomepageTextLength"] = homepage.text_length if homepage else 0
        output.at[row, "InternalPagesRequested"] = extraction.internal_pages_requested
        output.at[row, "InternalPagesHttpSucceeded"] = extraction.internal_pages_http_succeeded
        output.at[row, "InternalPagesSeleniumTried"] = extraction.internal_pages_selenium_tried
        output.at[row, "InternalPagesSeleniumSucceeded"] = extraction.internal_pages_selenium_succeeded
        output.at[row, "InternalPagesSucceeded"] = extraction.internal_pages_succeeded
        output.at[row, "TotalExtractedTextLength"] = extraction.total_text_length
        output.at[row, "SourceQuality"] = extraction.source_quality
        output.at[row, "PromptChars"] = extraction.prompt_chars
        output.at[row, "EstimatedInputTokens"] = extraction.estimated_input_tokens
        output.at[row, "ExactInputTokens"] = extraction.exact_input_tokens
        output.at[row, "ActualInputTokens"] = classification.input_tokens
        output.at[row, "OutputTokens"] = classification.output_tokens
        output.at[row, "EstimatedCostUsd"] = classification.estimated_cost_usd
        output.at[row, "TokenCountMethod"] = extraction.token_count_method
        output.at[row, "ModelName"] = classification.model_name or model_name
        output.at[row, "BatchId"] = classification.batch_id or batch_id
        output.at[row, "BatchStatus"] = classification.batch_status or batch_status
        output.at[row, "ModelError"] = classification.model_error
        output.at[row, "ProcessingTimeMs"] = extraction.processing_time_ms

    return output


# ===================== MAIN FLOW =====================
def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Classify website Niche/Categories/Language with Claude Batch API.")
    parser.add_argument("--input", default=DEFAULT_INPUT, help="Input Excel file path.")
    parser.add_argument("--output", default=DEFAULT_OUTPUT, help="Output Excel file path.")
    parser.add_argument("--url-column", default=DEFAULT_URL_COLUMN, help="Optional URL column name. Auto-detects Website/URL/Domain by default.")
    parser.add_argument("--checkpoint", default=DEFAULT_CHECKPOINT, help="Checkpoint JSON path.")
    parser.add_argument("--resume", action="store_true", help="Resume from checkpoint if it exists.")
    parser.add_argument("--max-sites", type=int, default=DEFAULT_MAX_SITES, help="Max rows to process. Default 20 to avoid accidental large paid runs.")
    parser.add_argument("--start-row", type=int, default=DEFAULT_START_ROW, help="Zero-based row offset in the Excel dataframe.")
    parser.add_argument("--concurrency", type=int, default=DEFAULT_CONCURRENCY, help="Concurrent website extraction workers.")
    parser.add_argument("--request-timeout", type=int, default=DEFAULT_REQUEST_TIMEOUT, help="HTTP request timeout seconds.")
    parser.add_argument("--internal-pages", type=int, default=DEFAULT_INTERNAL_PAGES, help="Max internal pages to fetch per site. 0 disables light crawler.")
    parser.add_argument("--min-text-chars", type=int, default=DEFAULT_MIN_TEXT_CHARS, help="Minimum homepage extracted text chars before considering text usable.")
    parser.add_argument("--min-internal-text-chars", type=int, default=DEFAULT_MIN_INTERNAL_TEXT_CHARS, help="Minimum internal page extracted text chars before using it.")
    parser.add_argument("--no-selenium", action="store_true", help="Disable Selenium fallback completely.")
    parser.add_argument("--selenium-timeout", type=int, default=DEFAULT_SELENIUM_TIMEOUT, help="Selenium page load / wait timeout seconds.")
    parser.add_argument("--selenium-hard-timeout", type=int, default=DEFAULT_SELENIUM_HARD_TIMEOUT, help="Hard timeout for one Selenium fetch.")
    parser.add_argument("--selenium-internal-fallback", action="store_true", help="Allow Selenium fallback for weak/failed internal pages.")
    parser.add_argument("--selenium-internal-limit", type=int, default=DEFAULT_SELENIUM_INTERNAL_LIMIT, help="Max internal pages per site that may use Selenium if fallback enabled.")
    parser.add_argument("--model", choices=MODEL_CHOICES, default=DEFAULT_MODEL, help="Claude model. Default claude-haiku-4-5.")
    parser.add_argument("--api-key", default=ANTHROPIC_API_KEY, help="Anthropic API key. Overrides ANTHROPIC_API_KEY constant and env var.")
    parser.add_argument("--max-tokens", type=int, default=DEFAULT_MAX_TOKENS, help="Max output tokens per site classification request.")
    parser.add_argument("--max-prompt-chars", type=int, default=DEFAULT_MAX_PROMPT_CHARS, help="Max user prompt characters per site before dropping internal pages/trimming text.")
    parser.add_argument("--count-tokens", action="store_true", help="Call Anthropic token counting API before batch creation and write ExactInputTokens.")
    parser.add_argument("--poll-interval", type=int, default=DEFAULT_POLL_INTERVAL, help="Batch polling interval seconds.")
    parser.add_argument("--submit-only", action="store_true", help="Create Anthropic batch and exit without polling/results.")
    parser.add_argument("--extract-only", action="store_true", help="Only fetch/crawl/extract and write analytics; do not call Anthropic.")
    parser.add_argument("--log-file", default=DEFAULT_LOG_FILE, help="Log file path.")
    parser.add_argument("--proxy", action="append", default=DEFAULT_PROXY, help="Proxy URL. Can be passed multiple times.")
    args = parser.parse_args()
    args.internal_pages = max(0, min(10, args.internal_pages))
    args.selenium_internal_limit = max(0, min(args.internal_pages, args.selenium_internal_limit))
    args.proxies = args.proxy or []
    return args


def serialize_extractions(extractions: List[SiteExtraction]) -> List[Dict[str, Any]]:
    return [asdict(x) for x in extractions]


def deserialize_page(data: Optional[Dict[str, Any]]) -> Optional[PageExtract]:
    return PageExtract(**data) if data else None


def deserialize_extractions(items: List[Dict[str, Any]]) -> List[SiteExtraction]:
    result = []
    for item in items:
        item = dict(item)
        item["homepage"] = deserialize_page(item.get("homepage"))
        item["internal_pages"] = [PageExtract(**p) for p in item.get("internal_pages", [])]
        result.append(SiteExtraction(**item))
    return result


def run_extraction(df: pd.DataFrame, url_col: str, args: argparse.Namespace) -> List[SiteExtraction]:
    selected = df.iloc[args.start_row : args.start_row + args.max_sites]
    tasks = [(idx, row[url_col]) for idx, row in selected.iterrows()]
    extractions: List[SiteExtraction] = []

    logging.info("Starting extraction: rows=%s concurrency=%s internal_pages=%s", len(tasks), args.concurrency, args.internal_pages)
    with ThreadPoolExecutor(max_workers=max(1, args.concurrency)) as executor:
        futures = {executor.submit(crawl_site, idx, url, args): idx for idx, url in tasks}
        for future in tqdm(as_completed(futures), total=len(futures), desc="Extracting"):
            idx = futures[future]
            try:
                extraction = future.result()
            except Exception as e:
                logging.error("Extraction failed for row %s: %s\n%s", idx, e, traceback.format_exc())
                extraction = SiteExtraction(
                    row_index=idx,
                    original_url=str(df.at[idx, url_col] if url_col in df.columns else ""),
                    normalized_start_url=normalize_input_url(df.at[idx, url_col] if url_col in df.columns else ""),
                    error=f"future_exception:{type(e).__name__}:{e}",
                    source_quality="failed",
                )
            extractions.append(extraction)

    extractions.sort(key=lambda x: x.row_index)
    return extractions


def log_usage_summary(classifications_by_row: Dict[int, ClassificationResult], model: str) -> None:
    input_tokens = sum(x.input_tokens for x in classifications_by_row.values())
    output_tokens = sum(x.output_tokens for x in classifications_by_row.values())
    estimated_cost = calculate_batch_cost_usd(model, input_tokens, output_tokens)
    logging.info(
        "Claude usage summary: actual_input_tokens=%s output_tokens=%s estimated_batch_cost_usd=%.6f",
        input_tokens, output_tokens, estimated_cost,
    )


def main() -> None:
    args = parse_args()
    setup_logging(args.log_file)

    logging.info("parser_v2 starting. model=%s max_sites=%s", args.model, args.max_sites)
    if not os.path.exists(args.input):
        raise RuntimeError(f"Input file not found: {args.input}")

    df = pd.read_excel(args.input)
    url_col = detect_url_column(df, args.url_column)

    checkpoint = load_checkpoint(args.checkpoint) if args.resume else {}
    batch_id = checkpoint.get("batch_id", "")
    batch_status = checkpoint.get("batch_status", "")
    custom_id_to_row = checkpoint.get("custom_id_to_row", {})

    if checkpoint.get("extractions"):
        logging.info("Loaded extractions from checkpoint: %s", len(checkpoint["extractions"]))
        extractions = deserialize_extractions(checkpoint["extractions"])
    else:
        extractions = run_extraction(df, url_col, args)
        save_checkpoint(
            args.checkpoint,
            {
                "version": 2,
                "model": args.model,
                "input": args.input,
                "output": args.output,
                "url_column": url_col,
                "extractions": serialize_extractions(extractions),
                "batch_id": "",
                "batch_status": "not_created",
                "custom_id_to_row": {},
            },
        )

    if args.extract_only:
        output = apply_results_to_dataframe(
            df,
            extractions,
            classifications_by_row={},
            model_name=args.model,
            batch_id="",
            batch_status="extract_only",
        )
        output.to_excel(args.output, index=False)
        logging.info("Extract-only output saved: %s", args.output)
        return

    classifications_by_row: Dict[int, ClassificationResult] = {}

    # For failed extraction rows, we do not send anything to Claude.
    failed_extractions = [x for x in extractions if x.source_quality == "failed" or not x.homepage]
    for extraction in failed_extractions:
        classifications_by_row[extraction.row_index] = empty_classification_for_extraction(
            extraction, model_name=args.model, batch_id=batch_id, batch_status=batch_status or "not_sent"
        )

    if not batch_id:
        client = create_anthropic_client(args.api_key)
        requests_payload, custom_id_map = create_batch_requests(
            extractions,
            model=args.model,
            max_tokens=args.max_tokens,
            max_prompt_chars=args.max_prompt_chars,
            client=client,
            count_tokens=args.count_tokens,
        )
        estimated_input_tokens = sum(x.estimated_input_tokens for x in extractions)
        exact_input_tokens = sum(x.exact_input_tokens for x in extractions)
        input_for_estimate = exact_input_tokens or estimated_input_tokens
        estimated_input_cost = calculate_batch_cost_usd(args.model, input_for_estimate, 0)
        logging.info(
            "Prepared Claude batch requests=%s estimated_input_tokens=%s exact_input_tokens=%s estimated_input_cost_usd=%.6f",
            len(requests_payload), estimated_input_tokens, exact_input_tokens, estimated_input_cost,
        )

        if not requests_payload:
            logging.warning("No valid extracted sites to send to Claude.")
            output = apply_results_to_dataframe(
                df,
                extractions,
                classifications_by_row=classifications_by_row,
                model_name=args.model,
                batch_id="",
                batch_status="no_valid_requests",
            )
            output.to_excel(args.output, index=False)
            return

        logging.info("Creating Anthropic batch with %s requests...", len(requests_payload))
        batch = submit_batch(client, requests_payload)
        batch_id = batch.id
        batch_status = batch.processing_status
        custom_id_to_row = {custom_id: extraction.row_index for custom_id, extraction in custom_id_map.items()}
        save_checkpoint(
            args.checkpoint,
            {
                "version": 2,
                "model": args.model,
                "input": args.input,
                "output": args.output,
                "url_column": url_col,
                "extractions": serialize_extractions(extractions),
                "batch_id": batch_id,
                "batch_status": batch_status,
                "custom_id_to_row": custom_id_to_row,
            },
        )
        logging.info("Batch created: %s status=%s", batch_id, batch_status)
        if args.submit_only:
            output = apply_results_to_dataframe(
                df,
                extractions,
                classifications_by_row=classifications_by_row,
                model_name=args.model,
                batch_id=batch_id,
                batch_status=batch_status,
            )
            output.to_excel(args.output, index=False)
            logging.info("Submit-only output saved with batch id: %s", args.output)
            return
    else:
        client = create_anthropic_client(args.api_key)
        # Recreate custom_id_map from checkpoint row mapping.
        extraction_by_row = {x.row_index: x for x in extractions}
        custom_id_map = {
            custom_id: extraction_by_row[row]
            for custom_id, row in custom_id_to_row.items()
            if row in extraction_by_row
        }
        logging.info("Resuming batch: %s", batch_id)

    ended_batch = poll_batch(client, batch_id, poll_interval=args.poll_interval)
    batch_status = ended_batch.processing_status
    save_checkpoint(
        args.checkpoint,
        {
            "version": 2,
            "model": args.model,
            "input": args.input,
            "output": args.output,
            "url_column": url_col,
            "extractions": serialize_extractions(extractions),
            "batch_id": batch_id,
            "batch_status": batch_status,
            "custom_id_to_row": custom_id_to_row,
        },
    )

    model_results = read_batch_results(
        client,
        batch_id=batch_id,
        batch_status=batch_status,
        custom_id_map=custom_id_map,
        model_name=args.model,
    )
    classifications_by_row.update(model_results)
    log_usage_summary(classifications_by_row, args.model)

    output = apply_results_to_dataframe(
        df,
        extractions,
        classifications_by_row=classifications_by_row,
        model_name=args.model,
        batch_id=batch_id,
        batch_status=batch_status,
    )
    output.to_excel(args.output, index=False)
    logging.info("Done. Output saved: %s", args.output)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        logging.warning("Interrupted by user.")
        raise
    except Exception as e:
        logging.error("Fatal error: %s\n%s", e, traceback.format_exc())
        raise
