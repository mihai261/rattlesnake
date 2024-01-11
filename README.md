# Requirements
* Python > 3.9
* dotnet > 6.0

# Steps to run

This project includes a small Python project which can be analyzed as a demo. Running the analyser is a two-step process:

First run the probe.py script:
```bash
python3 ./Rattlesnake/python_scripts/probe.py ./thesis-test/thesis --dest ./Rattlesnake/python_scripts
```

Compile .NET application:
```bash
dotnet build ./Rattlesnake
```

Run .NET application:
```bash
./Rattlesnake/bin/Debug/net6.0/Rattlesnake ./Rattlesnake/python_scripts/results/probe_results.json
```

Results are available in the /results directory.
