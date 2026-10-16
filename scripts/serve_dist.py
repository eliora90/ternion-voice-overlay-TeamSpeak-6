#!/usr/bin/env python3
"""Раздача файлов сборки по HTTP с поддержкой докачки.

Зачем: `python3 -m http.server` не умеет Range, поэтому оборванная загрузка
большого EXE начинается с нуля. Здесь поддержаны Range, HEAD и keep-alive.

Запуск:  python3 scripts/serve_dist.py [--root dist] [--port 8132] [--bind 0.0.0.0]
"""
from __future__ import annotations

import argparse
import os
import re
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer

RANGE_RE = re.compile(r"bytes=(?P<start>\d*)-(?P<end>\d*)$")
CHUNK = 1 << 20  # 1 МиБ: меньше системных вызовов на больших файлах


class RangeRequestHandler(SimpleHTTPRequestHandler):
    """Статика с корректным Content-Range, чтобы работала докачка (`curl -C -`)."""

    protocol_version = "HTTP/1.1"
    server_version = "TernionFiles/1.0"

    def send_head(self):
        self._payload_remaining = None  # соединение переиспользуется, сбрасываем прошлое значение
        path = self.translate_path(self.path)
        if os.path.isdir(path):
            return super().send_head()
        if not os.path.isfile(path):
            self.send_error(404, "Файл не найден")
            return None

        fileobj = open(path, "rb")
        try:
            stat = os.fstat(fileobj.fileno())
            size = stat.st_size
            start, end, status = 0, size - 1, 200
            match = RANGE_RE.match(self.headers.get("Range", "").strip())
            if match and (match.group("start") or match.group("end")):
                start_raw, end_raw = match.group("start"), match.group("end")
                if start_raw:
                    start = int(start_raw)
                    end = int(end_raw) if end_raw else size - 1
                else:  # bytes=-N: последние N байт
                    start = max(0, size - int(end_raw))
                status = 206
                if start >= size or end < start:
                    self.send_error(416, "Requested Range Not Satisfiable")
                    fileobj.close()
                    return None
                end = min(end, size - 1)

            length = end - start + 1
            self.send_response(status)
            self.send_header("Content-Type", self.guess_type(path))
            self.send_header("Content-Length", str(length))
            self.send_header("Accept-Ranges", "bytes")
            self.send_header("Last-Modified", self.date_time_string(stat.st_mtime))
            if status == 206:
                self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
            self.end_headers()
            if start:
                fileobj.seek(start)
            self._payload_remaining = length
            return fileobj
        except Exception:
            fileobj.close()
            raise

    def copyfile(self, source, outputfile):
        remaining = self._payload_remaining
        if remaining is None:  # листинг каталога и прочие ответы от базового класса
            return super().copyfile(source, outputfile)
        while remaining > 0:
            data = source.read(min(CHUNK, remaining))
            if not data:
                break
            outputfile.write(data)
            remaining -= len(data)

    def log_message(self, format, *args):  # noqa: A002 — имя параметра задано базовым классом
        print(f"{self.address_string()} {format % args}", flush=True)


def main() -> None:
    parser = argparse.ArgumentParser(description="Раздача файлов сборки с поддержкой докачки")
    parser.add_argument("--root", default="dist", help="каталог с файлами")
    parser.add_argument("--port", type=int, default=8132)
    parser.add_argument("--bind", default="0.0.0.0")
    args = parser.parse_args()

    root = os.path.abspath(args.root)
    handler = partial(RangeRequestHandler, directory=root)
    ThreadingHTTPServer.allow_reuse_address = True
    with ThreadingHTTPServer((args.bind, args.port), handler) as httpd:
        print(f"Отдаю {root} на http://{args.bind}:{args.port}/", flush=True)
        httpd.serve_forever()


if __name__ == "__main__":
    main()
