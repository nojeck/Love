# GitHub 원격 저장소 연결 가이드

## 📋 개요
이 가이드는 로컬 Git 저장소를 GitHub 원격 저장소에 연결하는 단계별 과정을 설명합니다.

---

## 🔧 Step 1: GitHub에서 새 저장소 생성

### 1.1 GitHub 접속
- [GitHub](https://github.com) 에 로그인합니다.
- 우측 상단의 **+** 아이콘 → **New repository** 클릭

### 1.2 저장소 설정
```
Repository name: LoveSimulation_sample
Description: AI Voice-Based Love Simulation Game with Unity & Python Backend
Visibility: Public (또는 Private)
Initialize this repository with: 
  ☐ Add a README file (체크 해제 - 이미 로컬에 있음)
  ☐ Add .gitignore (체크 해제 - 이미 생성됨)
  ☐ Choose a license (선택사항)
```

### 1.3 저장소 생성
**Create repository** 버튼 클릭

---

## 🔐 Step 2: GitHub 인증 설정 (Windows)

### 2.1 Git Credential Manager 설정 (권장)
PowerShell을 **관리자 권한**으로 열고 실행:

```powershell
# Git Credential Manager 설정
git config --global credential.helper manager-core

# 또는 Windows Credential Manager 사용
git config --global credential.helper wincred
```

### 2.2 GitHub Personal Access Token 생성 (선택사항)
비밀번호 대신 토큰 사용 시:

1. GitHub 로그인 → **Settings** → **Developer settings** → **Personal access tokens**
2. **Generate new token** 클릭
3. 토큰 이름: `LoveSimulation_Push`
4. 권한 선택:
   - ✅ `repo` (전체 저장소 접근)
   - ✅ `workflow` (GitHub Actions)
5. **Generate token** 클릭
6. 토큰 복사 및 안전한 곳에 저장 (다시 볼 수 없음)

---

## 📡 Step 3: 로컬 저장소 원격 연결

### 3.1 현재 원격 저장소 확인
PowerShell에서 프로젝트 폴더로 이동:

```powershell
cd c:\Users\555a\LoveSimulation_sample
git remote -v
```

**현재 상태:**
```
origin  https://github.com/YOUR_USERNAME/LoveSimulation_sample.git (fetch)
origin  https://github.com/YOUR_USERNAME/LoveSimulation_sample.git (push)
```

### 3.2 원격 저장소 URL 업데이트
YOUR_USERNAME을 실제 GitHub 사용자명으로 변경:

```powershell
# 기존 원격 저장소 제거
git remote remove origin

# 새 원격 저장소 추가 (HTTPS 방식)
git remote add origin https://github.com/YOUR_USERNAME/LoveSimulation_sample.git

# 또는 SSH 방식 (SSH 키 설정 필요)
# git remote add origin git@github.com:YOUR_USERNAME/LoveSimulation_sample.git
```

### 3.3 원격 저장소 확인
```powershell
git remote -v
```

**예상 출력:**
```
origin  https://github.com/YOUR_USERNAME/LoveSimulation_sample.git (fetch)
origin  https://github.com/YOUR_USERNAME/LoveSimulation_sample.git (push)
```

---

## 🚀 Step 4: 코드 푸시

### 4.1 기본 브랜치 설정
```powershell
# 현재 브랜치 확인
git branch

# 기본 브랜치를 main으로 설정 (이미 설정됨)
git branch -M main
```

### 4.2 코드 푸시
```powershell
# 첫 푸시 (upstream 설정)
git push -u origin main

# 이후 푸시
git push
```

### 4.3 인증 입력
- **HTTPS 방식**: GitHub 사용자명과 비밀번호 (또는 Personal Access Token) 입력
- **SSH 방식**: SSH 키 자동 사용

---

## ✅ Step 5: 푸시 확인

### 5.1 GitHub에서 확인
1. GitHub 저장소 페이지 접속
2. 파일 목록이 표시되는지 확인
3. **commits** 탭에서 커밋 히스토리 확인

### 5.2 로컬에서 확인
```powershell
# 원격 브랜치 확인
git branch -r

# 로그 확인
git log --oneline -5
```

---

## 🔄 Step 6: 이후 작업 흐름

### 6.1 변경사항 커밋
```powershell
# 변경사항 스테이징
git add .

# 커밋
git commit -m "설명: 변경 내용"

# 푸시
git push
```

### 6.2 여러 브랜치 작업
```powershell
# 새 브랜치 생성
git checkout -b feature/새기능

# 작업 후 커밋
git add .
git commit -m "feature: 새기능 추가"

# 푸시
git push -u origin feature/새기능

# GitHub에서 Pull Request 생성
```

---

## 🆘 문제 해결

### 문제 1: "fatal: remote origin already exists"
```powershell
# 기존 원격 저장소 제거
git remote remove origin

# 새로 추가
git remote add origin https://github.com/YOUR_USERNAME/LoveSimulation_sample.git
```

### 문제 2: "Permission denied (publickey)"
SSH 방식 사용 시:
```powershell
# SSH 키 생성 (처음 한 번만)
ssh-keygen -t ed25519 -C "your_email@example.com"

# GitHub Settings → SSH and GPG keys → New SSH key
# 공개 키 내용 복사 및 추가
```

### 문제 3: "fatal: Authentication failed"
```powershell
# 자격증명 초기화
git config --global --unset credential.helper

# 다시 설정
git config --global credential.helper manager-core

# 재시도
git push
```

### 문제 4: "Updates were rejected because the remote contains work"
```powershell
# 원격 변경사항 가져오기
git pull origin main

# 충돌 해결 후 푸시
git push
```

---

## 📚 유용한 명령어

```powershell
# 원격 저장소 상태 확인
git remote -v

# 원격 브랜치 확인
git branch -r

# 원격 변경사항 가져오기 (병합 안 함)
git fetch origin

# 원격 변경사항 가져오기 및 병합
git pull origin main

# 로컬 커밋 확인
git log --oneline

# 푸시 전 변경사항 확인
git status

# 마지막 커밋 수정
git commit --amend

# 푸시 이력 확인
git reflog
```

---

## 🎯 체크리스트

- [ ] GitHub 계정 생성 및 로그인
- [ ] GitHub에서 새 저장소 생성
- [ ] Git Credential Manager 설정
- [ ] 로컬 원격 저장소 URL 업데이트
- [ ] `git push -u origin main` 실행
- [ ] GitHub에서 파일 확인
- [ ] 첫 번째 푸시 성공 확인

---

## 📞 추가 도움말

- [GitHub 공식 문서](https://docs.github.com)
- [Git 공식 문서](https://git-scm.com/doc)
- [GitHub CLI 설치](https://cli.github.com)

---

**작성일**: 2026-03-25  
**버전**: 1.0
