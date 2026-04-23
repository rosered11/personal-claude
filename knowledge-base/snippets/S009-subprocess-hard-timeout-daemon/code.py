import subprocess
import threading
from datetime import timedelta

TIMEOUT_EXTRACT    = timedelta(minutes=30)
TIMEOUT_SUBPROCESS = int(TIMEOUT_EXTRACT.total_seconds()) - 120  # 28-min hard kill; 2-min buffer


def run_subprocess_with_timeout(cmd, cwd, env):
    proc = subprocess.Popen(
        cmd, cwd=cwd, env=env,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        text=True, bufsize=1,
    )

    def _stream_output():
        for line in proc.stdout:
            print(line, end="")

    stream_thread = threading.Thread(target=_stream_output, daemon=True)

    try:
        print("=== Subprocess Logging Start ===")
        stream_thread.start()

        try:
            proc.wait(timeout=TIMEOUT_SUBPROCESS)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait()
            raise Exception(
                f"Subprocess exceeded {TIMEOUT_SUBPROCESS // 60}-minute hard limit and was killed"
            )

        stream_thread.join(timeout=5)
        print("=== Subprocess Logging End ===")

        exit_code = proc.returncode
        print("Exit code:", exit_code)
        if exit_code != 0:
            raise Exception(f"Subprocess failed with exit code {exit_code}")

    except Exception:
        if proc.poll() is None:
            proc.kill()
            proc.wait()
        raise
    finally:
        if proc.poll() is None:    # safety net — always kill on any exit path
            proc.kill()
            proc.wait()
