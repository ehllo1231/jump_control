# Stage1 아트 제작 가이드

## 권장 계층

```text
Stage1
├── Collision
│   └── Floor
├── BackgroundDecor
├── ForegroundDecor
│   ├── StageArt
│   └── CollisionGuides
│       └── Floor
└── Lighting
```

현재 Stage1은 재디자인 시작 상태다. `Floor`의 충돌과 회색 가이드만 남아 있으며 `BackgroundDecor`, `StageArt`, `Lighting`은 비어 있다. 새 구조물은 이 역할 계층을 유지하면서 추가한다.

## 작업 원칙

1. `Collision`은 게임 플레이의 기준이다. 최종 스프라이트, 장식, 조명용 Renderer를 이 계층에 추가하지 않는다.
2. `StageArt`에는 Collider, Rigidbody2D, `Platform2D`를 추가하지 않는다. 한 스프라이트가 여러 Platform을 덮어도 된다.
3. `CollisionGuides`는 충돌 구조의 회색 임시 외형을 보존하는 참고 자료다. 현재는 `Floor` 가이드만 있으며 새 충돌 구조를 만들 때 대응하는 가이드를 같은 이름으로 추가한다.
4. 아트와 충돌은 더 이상 부모-자식 관계가 아니다. Collider를 이동하거나 크기를 바꿔도 아트가 자동으로 따라오지 않으므로 두 계층을 함께 확인한다.
5. 발판 윗면, 벽, 가시 같은 위험 요소의 시각적 경계는 실제 Collider와 오해가 없도록 맞춘다. 장식이 충돌면보다 과도하게 튀어나오거나 안쪽으로 들어가지 않게 한다.
6. `BackgroundDecor`에는 플레이어 뒤에 보이는 배경을, `StageArt`에는 플레이 표면을, 별도 전경 장식에는 플레이어보다 앞에 보여야 할 요소를 둔다.
7. 전경 스프라이트가 플레이어, 점프 화살표, 게이지 또는 위험 요소를 가리지 않도록 Sorting Layer와 Order를 확인한다.
8. 최종 아트를 추가한 뒤에는 `CollisionGuides`를 끄고 실제 게임 화면으로 점프, 착지, 벽 반동, 가시 충돌을 플레이 테스트한다.
9. 구조를 수정할 때 `CollisionGuides`나 `StageArt`를 삭제하는 것과 `Collision`을 삭제하는 것을 혼동하지 않는다. Collider 삭제는 게임 플레이 변경이다.
10. 파일은 구역 단위 이름을 사용한다. 예: `Area_03_Ground`, `Area_03_Pillars`, `Area_03_Foreground`.
11. 이전 Stage1 디자인과 그레이박스 파일은 재사용하지 않는다. 새 아트는 비율과 목표 해상도를 먼저 정한 뒤 빈 Stage1 전용 폴더에서 시작한다.

## 성능 및 에셋 주의사항

- 거대한 단일 이미지 하나보다 카메라 구간과 반복 사용 단위를 고려해 적절히 나눈다.
- 같은 구역은 가능한 한 같은 Material과 일관된 Pixels Per Unit을 사용한다.
- Android에서는 지나치게 큰 텍스처, 많은 반투명 레이어와 과도한 오버드로를 피한다.
- 텍스처 압축 후 가장자리 번짐, 필터링, 알파 경계와 메모리 사용량을 실제 기기에서 확인한다.
- 배경 이미지에 Collider를 붙여 플레이 표면으로 사용하지 않는다.
