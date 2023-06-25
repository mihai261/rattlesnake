# Programe necesare
* Python > 3.9
* dotnet > 6.0

# Pasi de rulare

In acest repository este inclus un proiect Python care va servi pe post de demo. Rularea aplicatiei se face in doua etape:

Se ruleaza scriptul probe.py:
```bash
python3 ./Rattlesnake/python_scripts/probe.py ./thesis-test/thesis --dest ./Rattlesnake/python_scripts
```

Se compileaza aplicatia .NET:
```bash
dotnet build ./Rattlesnake
```

Se ruleaza aplicatia .NET:
```bash
./Rattlesnake/bin/Debug/net6.0/Rattlesnake ./Rattlesnake/python_scripts/results/probe_results.json
```

Rezultatele se pot vedea in folderul /results