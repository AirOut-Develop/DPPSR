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

4. **결과 확인**
   - JSON 결과 텍스트 박스에서 분석 결과를 확인하거나 “JSON 복사” 버튼으로 클립보드에 복사할 수 있습니다.
   - 오류 발생 시 상태 메시지와 별도의 대화상자에 상세 내용이 표시됩니다 (라이선스 문제, tessdata 누락, 이미지 로드 실패 등).

## 참고 사항
- `MainWindow`는 테스트 목적으로 간단히 구성돼 있으므로, 실제 애플리케이션에 반영할 때는 UI/UX 요구사항에 맞게 수정하세요.
- 라이선스 검증 실패는 `RestLicenseRegistry`에서 `LicenseVerificationException`으로 처리하며, UI에서 잡아서 메시지로 출력합니다. 예외가 자주 발생하면 Visual Studio의 예외 설정(Thrown)을 조정하거나 서버 상태를 점검하세요.
- `MachineFingerprintProvider`는 Windows에서 WMI를 통해 CPU ID를 우선 가져오며, 실패 시 머신/도메인 이름 해시로 대체합니다.
- `CardAnalyzerOptions`의 `EnableAutoRotate180`, `MergeAdjacentTextBlocks`, `FieldDefinitionPath` 등의 옵션을 필요에 따라 조정해 OCR 결과를 튜닝할 수 있습니다.

## 지원 문의
내부 REST 서버와 라이선스 키 관련 문제는 서버 담당자에게 문의하세요. 나머지 OCR/분석 로직은 AOIDSClib DLL 버전을 확인한 뒤 이 문서를 토대로 셋업하면 됩니다.
