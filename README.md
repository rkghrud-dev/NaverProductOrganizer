# Naver Product Organizer

스마트스토어 상품을 네이버 커머스API에서 가져와 로컬 DB로 정리하고, 엑셀 기반으로 상품명 변경/중복 후보 정리/상품 묶기 후보 체크를 시작하는 WinForms 도구입니다.

## 현재 기능

- 여러 네이버 API 계정 저장
- `C:\Users\rkghr\Desktop\key` 폴더의 키 후보 스캔
- 전체 상품 목록 동기화 후 SQLite DB 저장
- 상품번호, 판매자상품코드, 상품명, 판매상태, 가격, 재고, 태그, 대표이미지 표시
- 판매자상품코드 `GS` + 숫자 7자리 기준 중복 표시
- 전체 상품 화면에서 중복만/할인만/판매상태 필터
- 상품명 변경용 엑셀 서식 생성
- 상품명 변경 엑셀 가져오기
- 네이버 원상품 수정 API로 상품명 적용
- 체크한 상품을 순서대로 열어 상품명을 직접 수정하고 즉시 적용
- 상품명 변경 시 원상품명과 스마트스토어 채널 전용 상품명(`channelProductName`)을 함께 적용
- 태그 변경용 엑셀 서식 생성
- 태그 변경 엑셀 가져오기
- 네이버 `detailAttribute.seoInfo.sellerTags`에 판매자 입력 태그 적용
- 할인 변경용 엑셀 서식 생성
- 네이버 `customerBenefit.immediateDiscountPolicy.discountMethod`에 즉시할인 적용
- 전체 상품에서 체크한 상품에 원/% 즉시할인 바로 적용
- 중복 상품 후보 분석
- 선택 중복 상품 삭제
- 상품 묶기 후보 체크

## 실행

```powershell
dotnet run
```

빌드 결과:

```text
bin\Debug\net10.0-windows\NaverProductOrganizer.exe
```

## 키 저장 형식

계정 저장에는 아래 값이 필요합니다.

- Client ID
- Client Secret
- 판매자 UID(account_id)

키 파일 자동 스캔은 `txt`, `json`, `md`, `.env` 파일에서 아래 이름을 찾습니다.

```text
client_id=...
client_secret=...
account_id=...
```

상품 API는 `SELLER` 토큰이 필요하므로 `account_id`가 비어 있으면 실제 동기화가 실패합니다.

## 데이터 위치

- DB: `data\products.sqlite`
- 엑셀 내보내기 기본 폴더: `data\exports`

## 안전 장치

상품명 변경과 중복 삭제는 실제 네이버 상품에 반영됩니다. 적용 전에는 `DB 엑셀 내보내기`로 백업한 뒤 진행하세요.

## 태그 변경 방식

`태그 변경 서식 만들기`로 엑셀을 만든 뒤 `new_tags` 열에 쉼표 또는 줄바꿈으로 태그를 입력합니다.

```text
스텐볼트, M2볼트, 육각렌치, 미니드라이버
```

프로그램은 중복 태그를 제거하고 상품당 최대 10개까지 정리해서 직접 입력 태그(`text`)로 반영합니다.

## 할인 변경 방식

기본 방식은 전체 상품 탭에서 상품을 체크한 뒤 `선택 할인 적용`을 누르는 것입니다.

- 할인 단위: `원` 또는 `%`
- 기간 설정은 선택 사항입니다.

엑셀로 대량 할인안을 준비해야 할 때는 할인 변경 서식을 사용할 수 있습니다.

- `new_discount_value`: 할인 값
- `new_discount_unit`: `PERCENT` 또는 `WON`
- `start_date`, `end_date`: 비워도 되고, `yyyy-MM-dd` 형식으로 넣으면 자동으로 네이버 날짜 형식으로 변환합니다.

네이버 커머스API 구조체 기준으로 즉시할인은 `customerBenefit.immediateDiscountPolicy.discountMethod`에 반영합니다.

## 다음 단계 후보

- 샵검색어/판매자 태그 자동 생성 및 제한 태그 검사
- 대표 이미지/추가 이미지 자동 교체
- 그룹상품 전환 실행
- 상품명 변경 결과 엑셀 리포트
