---
name: git-commit-code-changes
description: Commit Codex-made code changes after implementation. Use when the user requests a new change, bug fix, feature implementation, refactor, configuration update, or documented behavior change and Codex edits repository files; create a git commit for the completed requested work unless the user explicitly says not to commit or no files were changed.
---

# Git Commit Code Changes

## 원칙

사용자의 구현 요청으로 파일을 수정하면 작업 단위가 끝난 뒤 git commit을 남긴다. 커밋은 요청한 변경과 직접 관련된 파일만 포함하고, 기존에 있던 사용자 변경이나 현재 요청과 무관한 변경은 포함하지 않는다.

## 절차

1. 수정 전 `git status --short`로 기존 dirty 상태를 확인한다.
2. 요청한 변경을 구현하고 필요한 검증을 실행한다.
3. `git diff`와 `git status --short`로 커밋 대상이 요청한 작업에 한정되는지 확인한다.
4. 현재 요청과 관련된 Codex 변경 파일만 stage한다.
5. 간결한 핵심 요약으로 커밋한다.
6. 최종 응답에 커밋 해시와 검증 결과를 함께 보고한다.

## 커밋 메시지

커밋 메시지는 변경의 핵심을 한 줄로 요약한다.

예:

- `Improve jump reachability drag performance`
- `Add debug jump input dialog`
- `Document platform rotation use case`

사용자가 한국어로 요청했고 저장소 관례가 한국어라면 한국어 메시지도 사용할 수 있다. 메시지 본문은 필요한 경우에만 추가한다.

## 주의사항

- 사용자가 명시적으로 커밋하지 말라고 하면 커밋하지 않는다.
- 분석, 리뷰, 질문 답변처럼 파일을 수정하지 않은 작업은 커밋하지 않는다.
- 커밋 전에 검증을 시도한다. 검증을 실행하지 못했거나 실패했더라도 사용자가 변경 유지를 원하면 커밋할 수 있지만, 최종 응답에 제한 사항을 분명히 적는다.
- 기존 dirty 파일이 있으면 무관한 변경을 stage하지 않는다.
- 같은 파일 안에 기존 사용자 변경과 Codex 변경이 섞여 있어 안전하게 분리 stage할 수 없으면 커밋하지 말고 사용자에게 상황을 보고한다.
- git hook 또는 충돌로 커밋이 실패하면 실패 이유와 남은 변경 상태를 보고한다.
