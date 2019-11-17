@echo off

echo Setting profiler variables

set COR_ENABLE_PROFILING=1
set COR_PROFILER=ReactivityMonitor.RxProfiler.1

echo Running program

TestProfilee %*
