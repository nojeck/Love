@echo off
cd /d "C:\Users\555a\LoveSimulation_sample\LoveSimulation_plan"
call .venv\Scripts\activate.bat
pip install --upgrade pip
pip install python-pptx markdown requests
echo.
echo Installation complete!
pause
