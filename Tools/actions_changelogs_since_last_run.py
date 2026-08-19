#!/usr/bin/env python3

#
# Sends updates to a Discord webhook for new changelog entries since the last GitHub Actions publish run.
# Automatically figures out the last run and changelog contents with the GitHub API.
#

import io
import itertools
import os
import sys
import urllib.parse
from pathlib import Path
from typing import Any, Iterable

import requests
import yaml
import json
from typing import Any, Iterable

DEBUG = os.environ.get("SS14_CHANGELOG_DEBUG", "").lower() in {"1", "true", "yes"}
DEBUG_CHANGELOG_FILE_OLD = Path("Resources/Changelog/Old.yml")
DEBUG_DISCORD_DUMP_FILE = Path("Resources/Changelog/DiscordDebug.md")
GITHUB_API_URL = os.environ.get("GITHUB_API_URL", "https://api.github.com")

# https://discord.com/developers/docs/resources/webhook
DISCORD_SPLIT_LIMIT = 1800
DISCORD_WEBHOOK_URL = os.environ.get("DISCORD_WEBHOOK_URL")
TRUNCATION_SUFFIX = " [...]"

CHANGELOG_FILES = ["Resources/Changelog/Changelog.yml", "Resources/Changelog/ChangelogSyndie.yml", "Resources/Changelog/ChangelogVanilla.yml"] # Corvax-vanilla-MultiChangelog

TYPES_TO_EMOJI = {
    "Fix":    "🐛",
    "Add":    "✨", # Corvax: Use gitmoji 💥
    "Remove": "❌",
    "Tweak":  "⚒️"
}

ChangelogEntry = dict[str, Any]

def main():
    if not DEBUG and not DISCORD_WEBHOOK_URL:
        print("No discord webhook URL found, skipping discord send")
        return

    if DEBUG:
        # to debug this script locally, you can use
        # a separate local file as the old changelog
        last_changelog_stream = DEBUG_CHANGELOG_FILE_OLD.read_text(encoding="utf-8-sig")
    else:
        # when running this normally in a GitHub actions workflow,
        # it will get the old changelog from the GitHub API
        last_changelog_stream = get_last_changelog()

    last_changelog = yaml.safe_load(last_changelog_stream)
    with open(CHANGELOG_FILE, "r", encoding="utf-8-sig") as f:
        cur_changelog = yaml.safe_load(f)

    diff = diff_changelog(last_changelog, cur_changelog)
    message_lines = changelog_entries_to_message_lines(diff)

    if DEBUG:
        dump_debug_markdown(message_lines)
        return

    send_message_lines(message_lines)


def get_most_recent_workflow(sess: requests.Session) -> Any:
    workflow_run = get_current_run(sess)
    past_runs = get_past_runs(sess, workflow_run)
    for run in past_runs:
        return run

    raise RuntimeError("Could not find a previous successful workflow run")


def get_current_run(sess: requests.Session) -> Any:
    resp = sess.get(f"{GITHUB_API_URL}/repos/{GITHUB_REPOSITORY}/actions/runs/{GITHUB_RUN}")
    resp.raise_for_status()
    return resp.json()


def get_past_runs(sess: requests.Session, current_run: Any) -> Iterable[Any]:
    """
    Get all successful workflow runs before our current one.
    """
    params = {
        "status": "success",
        "created": f"<={current_run['created_at']}",
        "per_page": 100,
    }
    url = f"{current_run['workflow_url']}/runs"

    while url:
        resp = sess.get(url, params=params)
        resp.raise_for_status()

        for run in resp.json()["workflow_runs"]:
            # First past successful run that isn't our current run.
            if run["id"] == current_run["id"]:
                continue

            yield run

        next_url = resp.links.get("next", {}).get("url")
        if not next_url:
            break

        url = next_url
        params = None


def get_last_changelog() -> str:
    github_repository = os.environ["GITHUB_REPOSITORY"]
    github_run = os.environ["GITHUB_RUN_ID"]
    github_token = os.environ["GITHUB_TOKEN"]

    session = requests.Session()
    session.headers["Authorization"] = f"Bearer {github_token}"
    session.headers["Accept"] = "application/vnd.github+json"
    session.headers["X-GitHub-Api-Version"] = "2022-11-28"

    most_recent = get_most_recent_workflow(session, github_repository, github_run)
    last_sha = most_recent["head_commit"]["id"]
    print(f"Last successful publish job was {most_recent['id']}: {last_sha}")
    last_changelog_stream = get_last_changelog_by_sha(
        session, last_sha, github_repository
    )

    return last_changelog_stream


def get_last_changelog_by_sha(
    sess: requests.Session, sha: str, github_repository: str
) -> str:
    """
    Use GitHub API to get the previous version of the changelog YAML (Actions builds are fetched with a shallow clone)
    """
    params = {
        "ref": sha,
    }
    headers = {
        "Accept": "application/vnd.github.raw"
    }

    resp = sess.get(f"{GITHUB_API_URL}/repos/{GITHUB_REPOSITORY}/contents/{changelog_file}", headers=headers, params=params)
    resp.raise_for_status()
    return resp.text


def diff_changelog(old: dict[str, Any], cur: dict[str, Any]) -> Iterable[ChangelogEntry]:
    """
    Find all new entries not present in the previous publish.
    """
    old_entry_ids = {e["id"] for e in old["Entries"]}
    return (e for e in cur["Entries"] if e["id"] not in old_entry_ids)


def get_discord_body(content: str):
    return {
            "content": content,
            # Do not allow any mentions.
            "allowed_mentions": {
                "parse": []
            },
            # SUPPRESS_EMBEDS
            "flags": 1 << 2
        }

# Rayten-Localization-Start
def translate_to_russian(text: str) -> str:
    if not text.strip():
        return text

    try:
        resp = requests.get(
            "https://api.mymemory.translated.net/get",
            params={"q": text, "langpair": "en|ru"},
            timeout=5
        )
        resp.raise_for_status()
        data = resp.json()
        return data.get("responseData", {}).get("translatedText", text)
    except Exception as e:
        print(f"Translation failed: {e}")
        return text
# Rayten-Localization-End

def send_discord(content: str):
    try:
        original_length = len(content)
        if original_length > 2000:
            print(f"⚠️ Сообщение слишком длинное ({original_length} символов), обрезаем до 2000.")
            content = content[:2000]

        print(f"📤 Отправка чанка длиной {len(content)} символов...")

        body = get_discord_body(content)

        response = requests.post(DISCORD_WEBHOOK_URL, json=body)
        response.raise_for_status()
        print("✅ Успешно отправлено.")

    except requests.exceptions.HTTPError as http_err:
        print(f"❌ HTTP ошибка при отправке в Discord: {http_err}")
        if http_err.response is not None:
            try:
                print(f"🧾 Ответ Discord: {http_err.response.json()}")
            except Exception:
                print(f"🧾 Ответ Discord (сырой): {http_err.response.text}")

def truncate_to_limit(text: str, limit: int) -> str:
    if len(text) <= limit:
        return text

    if limit <= len(TRUNCATION_SUFFIX):
        return TRUNCATION_SUFFIX[:limit]

    return text[: limit - len(TRUNCATION_SUFFIX)].rstrip() + TRUNCATION_SUFFIX


def create_change_line(emoji: str, message: str, url: str | None) -> str:
    if url is None:
        prefix = f"{emoji} - "
        suffix = "\n"
    else:
        pr_number = urllib.parse.urlparse(url).path.rstrip("/").split("/")[-1]
        prefix = f"{emoji} - "
        suffix = f" ([#{pr_number}]({url}))\n"

    available_message_length = DISCORD_SPLIT_LIMIT - len(prefix) - len(suffix)
    if available_message_length < 1:
        raise ValueError(f"Rendered changelog line has no room for a message: {url}")

    message = truncate_to_limit(message, available_message_length)
    return f"{prefix}{message}{suffix}"


def changelog_entries_to_message_lines(entries: Iterable[ChangelogEntry]) -> list[str]:
    """Process structured changelog entries into a list of lines making up a formatted message."""
    message_lines = []

    except Exception as e:
        print(f"❌ Неизвестная ошибка при отправке в Discord: {e}")

def send_to_discord(entries: Iterable[ChangelogEntry]) -> None:
    if not DISCORD_WEBHOOK_URL:
        print(f"No discord webhook URL found, skipping discord send")
        return

    message_content = io.StringIO()

    for name, group in itertools.groupby(entries, lambda x: x["author"]):
        group_content = io.StringIO()
        group_content.write(f"**{name}** обновил(а):\n")

        for entry in group:
            for change in entry["changes"]:
                emoji = TYPES_TO_EMOJI.get(change['type'], "❓")
                message = change['message']
                url = entry.get("url")

                message_lines.append(create_change_line(emoji, message, url))

        group_text = group_content.getvalue()
        message_text = message_content.getvalue()
        message_length = len(message_text)
        group_length = len(group_text)

        if message_length + group_length >= DISCORD_SPLIT_LIMIT:
            print("📦 Превышен лимит, отправляем текущий блок.")
            send_discord(message_text)
            message_content = io.StringIO()

        message_content.write(group_text)

    message_text = message_content.getvalue()
    if len(message_text) > 0:
        print("✅ Финальная отправка")
        message_content.seek(0)
        for chunk in iter(lambda: message_content.read(2000), ''):
            if chunk.strip():  # чтобы не отправлять пустые чанки
                send_discord(chunk)


def split_message_lines(message_lines: list[str]) -> list[list[str]]:
    """Join message lines into chunks that are each below Discord's message length limit."""
    chunks = []
    chunk_lines = []
    chunk_length = 0

    for line in message_lines:
        line_length = len(line)
        if line_length > DISCORD_SPLIT_LIMIT:
            raise ValueError(
                f"Changelog line is too long for Discord after truncation: {line_length}"
            )

        new_chunk_length = chunk_length + line_length

        if new_chunk_length > DISCORD_SPLIT_LIMIT:
            if chunk_lines:
                chunks.append(chunk_lines)

            new_chunk_length = line_length
            chunk_lines = []

        chunk_lines.append(line)
        chunk_length = new_chunk_length

    if chunk_lines:
        chunks.append(chunk_lines)

    return chunks


def dump_debug_markdown(message_lines: list[str]):
    chunks = split_message_lines(message_lines)

    with DEBUG_DISCORD_DUMP_FILE.open("w", encoding="utf-8", newline="\n") as f:
        f.write("# Discord Changelog Debug Dump\n\n")
        f.write(
            f"Generated from `{DEBUG_CHANGELOG_FILE_OLD}` to `{CHANGELOG_FILE}`.\n\n"
        )

        if not chunks:
            f.write("_No changelog entries to send._\n")
            return

        for i, chunk_lines in enumerate(chunks, start=1):
            content = "".join(chunk_lines)
            f.write(
                f"<!-- Discord message break: chunk {i}/{len(chunks)}, {len(content)}/{DISCORD_SPLIT_LIMIT} characters -->\n\n"
            )
            f.write(f"## Discord Message {i}\n\n")
            f.write(content.lstrip("\n"))
            f.write("\n")

    print(f"Wrote Discord changelog debug dump to {DEBUG_DISCORD_DUMP_FILE}")


def send_message_lines(message_lines: list[str]):
    """Join a list of message lines into chunks that are each below Discord's message length limit, and send them."""
    chunks = split_message_lines(message_lines)

    for chunk_lines in chunks[:-1]:
        print("Split changelog and sending to discord")
        send_discord_webhook(chunk_lines)

    if chunks:
        print("Sending final changelog to discord")
        send_discord_webhook(chunks[-1])


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print(f"Failed to publish changelog to Discord: {e}", file=sys.stderr)
        exit(1)
