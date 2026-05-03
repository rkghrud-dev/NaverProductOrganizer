# Next Steps

다음 작업 시작 시 이 파일을 먼저 읽고 이어간다.

## 우선순위 1: 설치/업데이트/삭제 구조 개선

현재 `dist/NaverProductOrganizerSetup.exe`는 IExpress 기반 간단 설치 EXE다. 다음 버전은 진짜 설치 마법사 형태로 바꾼다.

- 설치 창에서 설치 경로 선택
- 바탕화면 바로가기 생성 옵션
- 시작 메뉴 등록
- 설치 완료 후 실행 옵션
- Windows 앱 제거/프로그램 제거 등록
- 다음 버전 설치 시 기존 설치 경로 감지
- 앱 내부 또는 설치파일 실행 시 업데이트/덮어쓰기 흐름 지원

권장 구현:

- Inno Setup 사용
- `installer/NaverProductOrganizer.iss` 추가
- 필요하면 Inno Setup 설치 여부 확인 후 없으면 설치 안내
- 빌드 산출물은 `dist/`에 두고 git에는 커밋하지 않음

## 우선순위 2: 상품 목록 우클릭 메뉴

현재 상단 버튼이 많아지고 있다. 다음에는 상품 목록 그리드에서 마우스 우클릭으로 대부분의 작업을 처리하게 만든다.

대상 탭:

- 전체 상품
- 상품명 변경
- 태그 변경
- 할인 변경
- 중복 후보

우클릭 메뉴 후보:

- 선택 상품명 수동변경
- 상품명 변경 적용
- 태그 변경 적용
- 선택 할인 적용
- 선택 중복 삭제
- 상품 묶기 후보 체크
- 선택 상품 DB 엑셀 내보내기
- 원상품번호/채널상품번호/판매자상품코드 복사
- 네이버 관리자 상품 수정 URL 열기
- 선택 행 새로고침/API 재조회

상단 버튼은 최소화한다.

- 전체 상품 동기화
- 화면 수정 저장
- DB 엑셀 내보내기
- 데이터 폴더 열기

## 우선순위 3: 다중 마켓 구조

추후 쿠팡 및 다른 스토어 API를 붙일 예정이다. 지금 네이버 전용 구조를 다중 마켓 구조로 확장해야 한다.

권장 방향:

- `MarketplaceAccount`
- `MarketplaceProduct`
- `IMarketplaceClient`
- `NaverCommerceClient : IMarketplaceClient`
- 이후 `CoupangClient`, `Cafe24Client`, `LotteOnClient` 추가

공통 상품 필드:

- 마켓
- 계정
- 원상품번호
- 채널상품번호/마켓상품번호
- 판매자상품코드
- 상품명
- 판매가
- 재고
- 판매상태
- 대표이미지
- 태그/키워드
- 할인 정보
- 원본 JSON

마켓별 차이는 별도 JSON 필드 또는 detail 테이블에 보관한다.

## 우선순위 4: 쿠팡 연동

쿠팡 API 키는 `C:\Users\rkghr\Desktop\key\coupang_wing_api.txt`에 있을 가능성이 높다. 키 내용은 노출하지 말고 형식만 확인한다.

먼저 구현할 기능:

- 쿠팡 계정 등록
- 쿠팡 상품 목록 조회
- 공통 DB에 저장
- 상품명/가격/재고 표시
- 네이버와 같은 중복 기준 적용 가능 여부 검토

주의:

- 쿠팡은 인증 방식이 네이버와 다르므로 별도 서명 모듈 필요
- API rate limit과 승인 범위를 먼저 확인

## 우선순위 5: 상품명 변경 안정화

네이버 상품명 변경은 다음 두 필드를 함께 바꿔야 한다.

- `originProduct.name`
- `smartstoreChannelProduct.channelProductName`

수동 변경 후 로그에서 `수동변경 성공/API확인` 여부를 확인한다.

## 최근 상태

- GitHub repo: `https://github.com/rkghrud-dev/NaverProductOrganizer`
- 최신 커밋 기준:
  - `a61ff8d Add Windows installer packaging`
  - 이 파일 추가 후 새 커밋이 생길 예정

