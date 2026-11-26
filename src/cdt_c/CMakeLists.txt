# cdt_c: a C interface over Artem Ogre's CDT, header-only, for C# to call. The C# side is
# CSharpCDT, built by its project.
cmake_minimum_required(VERSION 3.20)
project(cdt_c CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
if(NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE Release)
endif()
# One Mac library for Apple silicon and Intel.
if(APPLE)
  set(CMAKE_OSX_ARCHITECTURES "arm64;x86_64" CACHE STRING "" FORCE)
  set(CMAKE_OSX_DEPLOYMENT_TARGET "12.0" CACHE STRING "" FORCE)
endif()

add_library(cdt_c SHARED cdt_c.cpp)
target_include_directories(cdt_c PRIVATE
  ${CMAKE_CURRENT_SOURCE_DIR}/../../CDT/CDT/include
  ${CMAKE_CURRENT_SOURCE_DIR}/../../CDT/CDT)
# The library lands in build/bin for every configuration, so the C# build finds it in one place.
set_target_properties(cdt_c PROPERTIES
  CXX_VISIBILITY_PRESET hidden
  LIBRARY_OUTPUT_DIRECTORY $<1:${CMAKE_BINARY_DIR}/bin>
  RUNTIME_OUTPUT_DIRECTORY $<1:${CMAKE_BINARY_DIR}/bin>)
