@echo off

echo Setting profiler variables

set COR_ENABLE_PROFILING=1
set COR_PROFILER={09c5b5d7-62d2-4448-911d-2e1346a21110}
set COR_PROFILER_PATH_32=%cd%\..\..\..\..\ReactivityProfiler\bin\Win32\Debug\ReactivityProfiler.dll
set COR_PROFILER_PATH_64=%cd%\..\..\..\..\ReactivityProfiler\bin\x64\Debug\ReactivityProfiler.dll

echo Running program

TestProfilee %*
