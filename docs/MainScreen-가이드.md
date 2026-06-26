# MainScreen 스크립트 가이드 (UI 버튼 매핑용)

작성일: 2026-06-26
대상 파일: `Assets/Scripts/MainScreen.cs`
대상 독자: 씬에서 버튼 OnClick에 함수를 연결하는 UI 담당자

---

## 1. 버튼에 연결할 함수 목록 (제일 중요)

아래 메서드들은 **인스펙터의 Button > OnClick 드롭다운에서 바로 선택**할 수 있습니다.
(전부 `public void`, 인자 없음)

| 메서드 | 하는 일 | 어느 버튼에 연결 |
|--------|---------|------------------|
| `OpenLocker()` | **사물함(창고)** 화면 열기 | 하단 "사물함" 버튼 |
| `OpenMap()` | **지도(학교 도면)** 화면 열기 | 하단 "지도" 버튼 |
| `OpenDiary()` | **일기장** 화면 열기 | 하단 "일기" 버튼 |
| `OpenShop()` | **상점** 화면 열기 | 하단 "상점" 버튼 |
| `OpenStatus()` | **캐릭터 상태** 화면 열기 | 우상단 "상태보기" 버튼 |
| `GoHome()` | **돌아가기** — 캐릭터(기본) 화면으로 복귀 | 좌상단 "← 돌아가기" 버튼 |

> 위 6개 화면은 모두 **중앙 영역**에서 교체됩니다. 상단바와 하단 가방은 항상 그대로 남아 있습니다.

### 인스펙터에서 연결하는 법
1. 씬에서 버튼 선택 → Inspector의 `Button` 컴포넌트 찾기
2. `On Click ()` 박스의 `+` 클릭
3. 왼쪽 오브젝트 칸에 **MainScreen 컴포넌트가 붙은 오브젝트**(현재 이름: `MainScreen`)를 드래그
4. 오른쪽 함수 드롭다운 → `MainScreen` → 위 표의 메서드 선택
   - 예) 사물함 버튼이면 `MainScreen → OpenLocker ()`

---

## 2. 화면 흐름

```
            [캐릭터] (기본 화면, GoHome)
               │
   ┌───────┬───┴───┬───────┬─────────┐
 OpenLocker OpenMap OpenDiary OpenShop OpenStatus
   사물함    지도    일기     상점     상태보기
   (창고)  (학교도면) (더미)   (더미)   (스탯)
               │
            GoHome  ← 좌상단 '돌아가기'로 어느 화면에서든 캐릭터로 복귀
```

- `Open***()` 을 누르면 좌상단 **돌아가기 버튼이 자동으로 나타납니다.**
- `GoHome()`(캐릭터 화면)에서는 돌아가기 버튼이 숨겨집니다.
- `OpenLocker()`로 사물함을 열면 가방 패킹(드래그)이 활성화됩니다.

---

## 3. 기타 public 멤버 (프로그래머용 — 버튼에는 직접 못 씀)

| 멤버 | 설명 |
|------|------|
| `SetDay(int d)` | 상단 "DAY ??" 숫자 갱신 (인자가 있어 버튼 OnClick엔 코드로 연결) |
| `Bag` | 가방 모델(6×5 점유). `Bag.Contains("itemId")`, `Bag.Placed` 등 |
| `PlaceInBag(item, origin)` | 창고 아이템을 가방 좌표에 배치 (드래그 시스템이 호출) |
| `MoveToStorage(item)` | 가방 아이템을 창고로 회수 (드래그 시스템이 호출) |
| `LockerOpen` | 사물함이 열려 있는지 여부(읽기 전용) |

이 멤버들은 **드래그 인벤토리 내부 동작**에 쓰이므로 UI 버튼에 직접 연결할 필요는 없습니다.

---

## 4. 현재 구조 (✅ 씬 기반으로 전환 완료)

`MainScreen.cs`는 이제 **UI를 코드로 만들지 않습니다.** 대신 **씬에 미리 배치된 UI 오브젝트를 참조**만 합니다.
즉 디자이너가 씬에서 화면/버튼을 자유롭게 편집할 수 있고, 버튼 OnClick은 인스펙터에 **이미 연결돼 있습니다.**

### 씬 구성
- `MainCanvas` : 모든 UI가 이 아래에 있음 (상단바 · CenterArea(6개 화면) · BagFrame/BagGrid · Nav 버튼 · DragLayer)
- `MainScreen` (오브젝트) : 로직 컴포넌트. 인스펙터에 아래 참조가 연결돼 있음
  - 상단: `dayText`, `backButton`
  - 중앙 화면: `characterView / lockerView / mapView / diaryView / shopView / statusView`
  - 인벤토리: `bagGridRect`, `storageRect`, `dragLayer`

### UI를 다시 생성/초기화하려면
메뉴 **`NoPainYesGame > Build Main Screen UI`** 실행 → 씬에 UI를 새로 깔고 참조·버튼 OnClick을 자동 연결합니다.
(기존 `MainCanvas`는 지우고 새로 만듦. 디자이너가 손본 레이아웃을 갈아엎으니, 평소 편집은 씬에서 직접 하세요.)

### 디자이너가 할 일
- 씬에서 버튼/패널의 위치·크기·색·아트를 자유롭게 편집
- 버튼은 이미 OnClick이 연결돼 있음(예: 사물함 버튼 → `MainScreen.OpenLocker`). 새 버튼을 추가하면 1번 섹션 방법으로 직접 연결
- **주의**: 가방 격자(`BagGrid`)와 창고(`Storage`) 컨테이너는 드래그 좌표 계산에 쓰이므로 **pivot(좌상단)과 셀 크기(135) 기준**을 바꾸지 마세요. 칸 이미지의 색/모양만 바꾸면 OK

### 폰트 / 스프라이트
- 한글 폰트: `Assets/Fonts/malgun.ttf` (※ Windows 시스템 폰트 — **정식 배포 전 나눔고딕 등 라이선스 폰트로 교체** 권장)
- 단색 스프라이트: `Assets/UI/white.png`

---

## 5. 한눈 요약

- 버튼에 연결할 함수: **`OpenLocker / OpenMap / OpenDiary / OpenShop / OpenStatus / GoHome`** 6개 (인자 없는 `public void`).
- UI는 **씬에 실제로 배치돼 있고** 버튼 OnClick도 **이미 연결됨** → 디자이너는 씬에서 바로 편집.
- UI를 갈아엎고 새로 깔려면 메뉴 `NoPainYesGame > Build Main Screen UI`.
