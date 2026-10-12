## Arquitectura y Design Level System: PÁRAMO-NEXUS

Para llevar esta idea a Unity sin caer en un mapa caótico, la estructura se debe basar en principios probados de **Level Design de Áreas Abiertas** (estilo *Mad Max*, *Crossout* y mapas asimétricos de *PVP/PVE de Gran Escala*).

El modelo funcional se define como **Diseño en Embudo Asimétrico Radial con Nodos de Intersección Cruzada (Cross-Convergence Dynamic)**.

---

## 1. Métrica, Dimensiones y Grid

* **Tamaño Total del Terreno:** $4000\text{m} \times 4000\text{m}$ ($16\text{ km}^2$).
* **Densidad por Equipo:** $10\text{ Vehículos de Combate} + 1\text{ Payload Heavy-Rig}$ (Masa pesada, baja aceleración, $100.000\text{ HP}$).
* **Tiempo Objetivo de Partida:** 15 a 20 minutos.
* **Layout Topology:** 4 Spawns cardinales situados en las esquinas ($N, E, S, W$), con el **MONOLITO** fijado exactamente en el centro matemático ($0,0,0$), elevado en una meseta volcánica.

---

## 2. Los 4 Spawns (Asimetría de Terreno e Identidad)

Cada punto de salida define la composición ideal de la flota inicial y las mecánicas de avance.

| Equipo / Spawn | Bioma de Salida | Desafío Inicial (Early Game) | Recompensa de Ruta |
| --- | --- | --- | --- |
| **Team 1: Norte** | **Las Montañas / Picos Altos** | Descensos pronunciados, paso angosto, riesgo de volcar el Payload.

 | Posición de altura para Snipers/Artillería.

 |
| **Team 2: Sur** | **Tierras Olvidadas / Páramo Tóxico** | Ríos de lodo/ácido que reducen la tracción y velocidad en un $40\%$.

 | Ruta más corta en distancia en línea recta al centro.

 |
| **Team 3: Este** | **Cañón Rojo / Acantilados** | Abismos, saltos de fe y choked points estrechos.

 | Cobertura natural contra proyectiles de larga distancia.

 |
| **Team 4: Oeste** | **El Valle Bajo / Desierto de Dunas** | Campos minados reactivos y visibilidad reducida por tormentas.

 | Terreno plano para máxima velocidad de convoys pesados.

 |

---

## 3. Nodos de Intersección y Mecánicas de Interacción (Level Design)

El mapa no usa paredes invisibles; utiliza **Mecánicas de Terreno Guiadas por Clases de Vehículo (Role-Based Utility)**.

### A. Los Obstáculos de Clase (Gatekeeping Layout)

1. **Paredes de Roca Masiva (Rammer Gates):**
* **Mecánica:** Bloquean el paso a rutas de atajo hacia los valles.


* **Requisito:** Requiere que el vehículo clase *Rammer* (Tanque/Embestidor) golpee la estructura a $>80\text{ km/h}$ para destruirla. Abre una ruta limpia para el Payload.




2. **El Gran Abismo (The Chasm):**
* **Mecánica:** Una grieta tectónica de $30\text{ metros}$ de ancho.


* **Requisito:** Una *Bike* o *Scout* ultra rápido debe usar la rampa, saltar al otro lado y activar una terminal que despliega un puente levadizo para el convoy.




3. **Cueva Subterránea / Nido de Arañas:**
* **Mecánica:** Atajo subterráneo sin luz que esquiva los ataques de artillería del Cañón.


* **Amenaza:** Fauna nativa (Arácnidos biomecánicos) que atacan las ruedas/orugas e inmovilizan los vehículos. Se necesita fuego (Vehículos con Lanzallamas/Incendiarios) para despejar la infestación rápidamente.





```
       [TEAM 1 - MONTAÑAS]                  [TEAM 3 - CAÑÓN ROJO]
               \                                    /
                \                                  /
                 v                                v
            (Choke N)                        (Choke E)
                 \                                /
                  \                              /
                   +----> [ANILLO EXTERIOR] <---+
                                 |
                                 v
                     [EL MONOLITO (ZONA FINAL)]
                                 ^
                                 |
                   +----> [ANILLO EXTERIOR] <---+
                  /                              \
                 /                                \
            (Choke W)                        (Choke S)
                ^                                  ^
               /                                    \
       [TEAM 4 - VALLE]                     [TEAM 2 - T. TÓXICAS]

```

---

## 4. Estructura de Fases del Mapa (Macro-Loop)

Para evitar la monotonía del Battle Royale y mantener la tensión, el mapa se divide en 3 Anillos de Progresión Terrestre:

### Fase 1: Salida e Integración de Flota (0k m - 1.5 km)

* **Objetivo:** Sobrevivir al mapa.
* **Dinámica:** Cada equipo opera en su propio bioma. No hay combate PVP inmediato; el riesgo es la navegación, la coordinación para superar obstáculos de clase (Ramas, Abismos, Ácido) y no destruir el Payload contra el mapa.



### Fase 2: Puntos de Enfrentamiento Cruzado / Intersección (1.5 km - 3 km)

* **Objetivo:** Detener o ralentizar a los convoys rivales.
* **Mecánica "X-Crossing":** Las rutas geográficas están diseñadas en forma de paso a desnivel y cruces de vías.


* *Ejemplo:* El equipo de la Montaña pasa por un puente superior mientras el equipo del Valle pasa justo por debajo en el lecho del río. Se abren ventanas de fuego cruzado directo entre convoys.


* **Campos Minados y Nodos de Recursos:** Puntos estratégicos donde los convoyes deben detenerse a desactivar minas o capturar escudos térmicos temporales para el Payload.



### Fase 3: La Caldera del Monolito (Centro 1 km²)

* **Objetivo:** Descarga del Payload.
* **Topografía:** Un cráter masivo con 4 accesos principales y un anillo perimetral superior.


* **Dinámica:** La zona final no es plana. El **MONOLITO** está sobre una plataforma que requiere que el Payload permanezca inmóvil durante 60 segundos transmitiendo datos. Los 4 equipos sobrevivientes colisionan en un arena circular con cobertura destructible.

---

## 5. Implementación Técnica para Unity

Para el desarrollo en Unity, la arquitectura del mapa debe levantarse bajo estos pilares:

1. **Terrain System:**
* Usar **Unity Terrain Tile System** ($2\times2$ tiles de $2040\text{m}$) o **MicroSplat** para optimizar el shader de textura multitextura (Nieve/Roca, Lodo Tóxico, Arena, Basalto).




2. **Navegación del Payload (AI/Physics):**
* El Payload debe utilizar un sistema de **Spline-Based Pathfinding** con desviaciones dinámicas. No va sobre rieles fijos, pero los conductores (IA o jugadores) tienen "Guías NavMesh" ajustables según los obstáculos destruidos (ej. la piedra destruida abre una sub-malla NavMesh).


3. **Destructibilidad de Obstáculos:**
* Utilizar **RayFire** o mallas descompuestas (Fractured Meshes) con disparadores de físicas por umbral de masa/velocidad ($F = m \cdot a$) para garantizar que solo el *Rammer* pueda romper la roca.




4. **Cámara y Visibilidad:**
* Sistema de **Fog of War** dinámico o Tormentas de Polvo por zonas (Volumetric Fog) para ocultar a los equipos rivales hasta que entren en el rango de radar de las unidades *Scout*.