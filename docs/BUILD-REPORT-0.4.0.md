# 0.4.0 검증 기록

- C# WPF Release 빌드: 로컬 교차 빌드 및 GitHub Windows 빌드 통과.
- 포함 FFmpeg: gfxcapture 필터의 hwnd / resize_mode 지원 확인.
- 회귀 검사: 창 HWND만 지정, desktop/모니터 fallback 없음, 최초 캔버스 유지, 음수 모니터 좌표 유지, 잘못된 핸들 거부.
- 정지 시간 보정: 0.1초 정지 입력을 2.4초로 보정하고 ffprobe 2.400000초 확인.
- 실제 창 격리 시험: 빨간 대상 창을 파란 별도 창으로 가린 뒤 결과 중심 픽셀을 비교하는 테스트 추가.
- GitHub Windows 실행 환경에서 Failed to create D3D11VA device로 실제 창 격리 시험을 수행할 수 없었음. 정확히 이 환경 오류만 exit 77 / SKIP으로 분류. 다른 오류나 픽셀 불일치는 빌드 실패.
- 일반 PC에서 창 이동·크기 변경·최소화·닫힘·일시정지 재개, 브라우저 탭 전환, 오디오 동기화는 추가 검증 필요.
- PC 소리는 전역 루프백이며 앱별 오디오 캡처는 구현하지 않음. 창 모드로 전환하면 PC 소리를 끄고 사용자에게 범위를 안내.
- Windows.Graphics.Capture 엔진 문서: https://ffmpeg.org/ffmpeg-filters.html#gfxcapture
