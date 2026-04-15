from __future__ import annotations

from pathlib import Path

from .models import SelectedFiles

IGNORED_FILES = {".gitkeep", ".ds_store", "thumbs.db", "desktop.ini"}


def _list_files(folder: Path) -> list[Path]:
    if not folder.exists():
        return []
    return sorted(
        p for p in folder.iterdir()
        if p.is_file() and p.name.lower() not in IGNORED_FILES
    )


def _pick_one(files: list[Path], category: str) -> Path | None:
    if not files:
        print(f"  No {category} files found.")
        return None

    print(f"\n  Available {category}:")
    for i, f in enumerate(files, 1):
        print(f"    {i}. {f.name}")
    print(f"    0. None")

    while True:
        choice = input(f"  Select {category} [0-{len(files)}]: ").strip()
        if choice == "0":
            return None
        try:
            idx = int(choice)
            if 1 <= idx <= len(files):
                return files[idx - 1]
        except ValueError:
            pass
        print(f"  Invalid choice. Enter 0-{len(files)}.")


def _pick_many(files: list[Path], category: str) -> list[Path]:
    if not files:
        print(f"  No {category} files found.")
        return []

    print(f"\n  Available {category}:")
    for i, f in enumerate(files, 1):
        print(f"    {i}. {f.name}")
    print(f"    0. None")

    while True:
        choice = input(f"  Select {category} (comma-separated, or 0 for none): ").strip()
        if choice == "0":
            return []
        try:
            indices = [int(x.strip()) for x in choice.split(",")]
            if all(1 <= i <= len(files) for i in indices):
                return [files[i - 1] for i in indices]
        except ValueError:
            pass
        print(f"  Invalid input. Use comma-separated numbers (1-{len(files)}) or 0.")


def select_files(documents_dir: Path) -> SelectedFiles:
    print("\n=== Document Selection ===")

    cv = _pick_one(_list_files(documents_dir / "CVs"), "CV")
    letter = _pick_one(_list_files(documents_dir / "PersonalLetters"), "personal letter")
    other = _pick_many(_list_files(documents_dir / "Other"), "other files")

    selected = SelectedFiles(cv_path=cv, personal_letter_path=letter, other_paths=other)
    print(f"\nSelected files:\n{selected.display()}")
    return selected
