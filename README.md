# DPPSR Sample App 사용 가이드

이 문서는 `DPPSR` WPF 테스트 애플리케이션에서 AOIDSClib DLL을 활용하여 OCR 분석과 REST 기반 라이선스 검증을 수행하는 방법을 정리한 것입니다.

## 사전 준비

1. **라이브러리 DLL**
   - `AOIDSClib.dll` (최신 빌드) 파일을 `DPPSR/lib/` 폴더에 복사합니다. (이미 포함돼 있다면 최신 버전으로 교체하세요.)
   - 프로젝트는 파일 참조 방식으로 `lib\AOIDSClib.dll`을 참조합니다.

2. **필수 리소스**
   - `tessdata/` 폴더: Tesseract `eng`, `kor`, `jmc`, `jmd`, `mrzf` 등 필요한 언어 데이터를 실행 경로에 둡니다.
   - (선택) `Configuration/fields.json`: 커스텀 필드 좌표를 사용하려면 해당 파일을 준비하고 `CardAnalyzerOptions.FieldDefinitionPath`를 설정합니다.

3. **라이선스 서버**
   - REST 엔드포인트는 배포 환경에 따라 별도로 지정해야 합니다.
   - 서버에는 사용하려는 라이선스 키가 사전에 등록되어 있어야 하며, 첫 요청 시 CPU ID가 바인딩됩니다.

## 프로젝트 설정 확인
- `DPPSR.csproj`에는 다음 NuGet 패키지가 포함되어야 합니다: `OpenCvSharp4`, `OpenCvSharp4.runtime.win`, `OpenCvSharp4.Extensions`, `Tesseract`, `Newtonsoft.Json`.
- 라이선스 검증에 필요한 참조는 `AOIDSClib.Licensing` 네임스페이스로 이미 포함돼 있습니다.
- `MainWindow`에는 간단한 UI가 구성되어 있으며, 이미지를 불러오고 분석 결과 JSON을 확인할 수 있는 테스트용 환경입니다.

## 실행 절차

1. **라이선스 키 입력**
   - 화면 우측 상단의 “라이선스 키” 텍스트박스에 유효한 키를 입력하고 “적용” 버튼을 누릅니다.
   - `RestLicenseRegistry`가 자동으로 `/license/verify` 엔드포인트에 POST 요청을 보내며,
     - 성공 시 “라이선스 검증 성공. 분석을 실행할 수 있습니다.” 메시지가 표시됩니다.
     - 실패 시 서버 응답에 따라 이유를 안내합니다 (예: `라이선스 키가 존재하지 않습니다.`).

2. **이미지 불러오기**
   - “이미지 불러오기” 버튼을 클릭해서 `*.png`, `*.jpg` 등 OCR 대상 이미지를 선택합니다.
   - 선택한 이미지가 왼쪽 미리보기 창에 표시됩니다.

3. **분석 실행**
   - 라이선스가 검증되고 이미지가 선택된 상태에서 “분석 실행”을 누르면 OCR 분석이 수행됩니다.
   - 결과 JSON에는 운전면허증 여부 등 문서 판별 정보가 `{ "result": true/false, "type": "DriveLicence" }` 형태로 표시됩니다.
   - 기재사항(주민등록번호·이름·성인 여부)까지 필요하면 `ToFullJson()` 또는 `Identity` 속성을 사용합니다. 아래 **신분증 기재사항 인식** 참고.

4. **결과 확인**
   - JSON 결과 텍스트 박스에서 분석 결과를 확인하거나 “JSON 복사” 버튼으로 클립보드에 복사할 수 있습니다.
   - 오류 발생 시 상태 메시지와 별도의 대화상자에 상세 내용이 표시됩니다 (라이선스 문제, tessdata 누락, 이미지 로드 실패 등).

## 신분증 기재사항 인식

주민등록번호·이름·성인 여부를 함께 얻을 수 있습니다. 번호는 **체크섬과 실존 날짜 검증을 모두 통과한 경우에만** 채워집니다.

```csharp
using var analyzer = new CardAnalyzer(options);
var result = await analyzer.ClassifyDocumentAsync(imagePath);

if (result.Identity.Recognized)
{
    result.IdNumber;    // "9606091860218" (하이픈 없는 13자리)
    result.BirthDate;   // 1996-06-09 (성별코드로 세기 판정)
    result.Name;        // "김보석"
    result.IsAdult;     // true
}
else
{
    result.Identity.Error;   // RRN_NOT_FOUND / RRN_INVALID / NO_TEXT
}
```

### 성인 판정 기준

`IsAdult` 는 만 나이가 아니라 **연 나이**(현재 연도 − 출생 연도)가 19세 이상인지를 봅니다. 청소년보호법 기준입니다.
2026년이라면 2007년생은 성인, 2008년생은 미성년입니다.

검증에 실패한 번호는 언제나 `false` 입니다. **읽지 못한 것을 통과시키지 않습니다.**

번호만 따로 검증하려면 `AOIDSClib.Recognition.RrnValidator` 를 직접 쓸 수 있습니다.

```csharp
RrnValidator.IsChecksumValid("9606091860218");            // true
RrnValidator.TryGetBirthDate("9606091860218", out var b); // b = 1996-06-09
RrnValidator.IsAdult(b);                                  // true
```

### 투입 방향

카드를 세로로 넣든 거꾸로 넣든 같은 결과가 나옵니다. 축소본으로 네 방향을 먼저 훑어 유망한 순서를 정한 뒤
원본을 그 순서로 읽으므로, 버릴 방향을 원본 해상도로 OCR 하지 않습니다.
문서 종류 판별도 채택된 방향의 결과로 하므로, 세로 투입 시 분류가 실패하던 문제가 함께 해소됩니다.

### 인식 옵션

| 옵션 | 기본값 | 설명 |
|---|---|---|
| `EnableIdentityRecognition` | `true` | 끄면 예전처럼 0도 한 방향만 읽습니다 |
| `OrientationCandidates` | `{0,180,90,270}` | 시도 순서 |
| `StopAtFirstValidOrientation` | `true` | 검증 통과 시 남은 방향 생략 |
| `EnableOrientationPrescan` | `true` | 축소본 사전탐색 |
| `PrescanScale` | `0.35` | 사전탐색 축소 비율 |
| `TrimDarkBorder` | `true` | 실패 시 가장자리 검은 여백을 잘라내고 재시도 |

### 알아 둘 점

- **이름은 참고값입니다.** 흐린 IR 스캔본에서 한 글자를 놓칠 수 있습니다(실측에서 `김보석` → `경보석`).
  본인 확인 근거로는 검증을 통과한 번호를 쓰세요.
- `kor` 언어 데이터가 있어야 이름 인식이 동작합니다. 없으면 이름만 비고 번호 인식은 정상 동작합니다.
- 주민등록번호가 없는 면(예: 운전면허증 영문 뒷면)은 `RRN_NOT_FOUND` 로 정상 처리됩니다.

## 참고 사항
- `MainWindow`는 테스트 목적으로 간단히 구성돼 있으므로, 실제 애플리케이션에 반영할 때는 UI/UX 요구사항에 맞게 수정하세요.
- 라이선스 검증 실패는 `RestLicenseRegistry`에서 `LicenseVerificationException`으로 처리하며, UI에서 잡아서 메시지로 출력합니다. 예외가 자주 발생하면 Visual Studio의 예외 설정(Thrown)을 조정하거나 서버 상태를 점검하세요.
- `MachineFingerprintProvider`는 Windows에서 WMI를 통해 CPU ID를 우선 가져오며, 실패 시 머신/도메인 이름 해시로 대체합니다.
- `CardAnalyzerOptions`의 `EnableAutoRotate180`, `MergeAdjacentTextBlocks`, `FieldDefinitionPath` 등의 옵션을 필요에 따라 조정해 OCR 결과를 튜닝할 수 있습니다.
- 문서 종류 문자열이 규격대로(`DriveLicence`, `ResidentRegistration`, `ForeignerRegistration`, `Passport`, `none`) 나가도록 직렬화가 수정되었습니다. 이전 빌드는 C# 식별자(`DriverLicense` 등)를 내보냈으므로, 그 문자열에 맞춰 둔 코드가 있다면 확인이 필요합니다. 읽을 때는 양쪽 표기를 모두 받습니다.

## 지원 문의
내부 REST 서버와 라이선스 키 관련 문제는 서버 담당자에게 문의하세요. 나머지 OCR/분석 로직은 AOIDSClib DLL 버전을 확인한 뒤 이 문서를 토대로 셋업하면 됩니다.
