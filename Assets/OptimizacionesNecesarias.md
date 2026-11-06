1) En cambio de que haya tantos prefabs con PS anidados, estaría bueno que para PS como el muzzle de la planta carnivora, o el hit de su bala, o el choque del escarabajo, haya un ParticleSystemFactory que guarde objetos de tipo GenericPS.cs (este sería el script que el objeto padre de los PSs tiene como componente, y todos l0os PS correspondientes quedan como child object.

2) LittleBarrier y PurificationCore son en esencia lo mismo, unificar.

3) Atlas UI para todoooo

4) Reemplazar ints por ushort donde sea posible/necesario

5) CarniborousPlant no es más SOLID ni MVC, corregir (maldito Claudio)

3) BugDetection es un script de uso génerico pero su nombre no lo representa, cambiar nombre en todas las clases que lo invocan