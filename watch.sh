#!/bin/bash
# Helper script to run dotnet watch from repo root
cd "$(dirname "$0")/4-Nine"
exec dotnet watch "$@"
