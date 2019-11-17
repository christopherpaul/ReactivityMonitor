@echo off

set COR_ENABLE_PROFILING=1
set COR_PROFILER=ReactivityMonitor.RxProfiler.1

TestProfilee %*
