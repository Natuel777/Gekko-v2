# Splines Sandbox

Pruebas de `com.unity.splines` (ya viene con el proyecto via Cinemachine). Ver el documento
"Splines en Gekko - opciones de uso" para el resumen completo.

Nada acá toca el gameplay real; es para que el equipo pruebe y decida que vale la pena llevar
a los niveles.

- `Runtime/SplineSampledLine.cs` - alimenta un LineRenderer muestreando una spline
  (`SplineUtility.EvaluatePosition`). Es el camino de integracion de bajo riesgo para
  reemplazar `LineController.cs` (nucleo <-> enredadera) por una curva en vez de segmentos rectos.

Escena de prueba: `Assets/Scenes/Pruebas/SplinesTest.unity`.
