# SlendyTubies Multiplayer — Arquitectura & Plan de Desarrollo
**Arquitectura: Player-Host · TCP/UDP · Unity**

---

## 1. Estructura de carpetas del proyecto

```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Core/                        ← Núcleo del sistema
│   │   │   ├── GameManager.cs           # Estado global autoritativo (solo host)
│   │   │   ├── GameStateMachine.cs      # Lobby → Playing → GameOver → Results
│   │   │   ├── SceneLoader.cs           # Carga de escenas sin interrumpir red
│   │   │   └── Constants.cs            # Puertos, tiempos, tamaños de buffer
│   │   │
│   │   ├── Network/                     ← Capa de red completa
│   │   │   ├── Host/
│   │   │   │   ├── HostNetworkManager.cs    # Servidor TCP + UDP listener
│   │   │   │   ├── ClientSession.cs         # Representa cada cliente conectado
│   │   │   │   ├── ConnectionRegistry.cs    # Registro de sesiones activas
│   │   │   │   └── BanList.cs               # Lista de IPs baneadas (persistente)
│   │   │   ├── Client/
│   │   │   │   ├── ClientNetworkHandler.cs  # Conexión al host (TCP + UDP)
│   │   │   │   ├── PingMonitor.cs           # Medición de latencia RTT
│   │   │   │   └── ReconnectHandler.cs      # Reintento de conexión
│   │   │   ├── Shared/
│   │   │   │   ├── MessageQueue.cs          # Queue thread-safe (in/out)
│   │   │   │   ├── PacketSerializer.cs      # Serialización binaria de mensajes
│   │   │   │   ├── NetworkMessage.cs        # Base class + tipos de mensaje
│   │   │   │   └── NetworkProtocol.cs       # Enum de OpCodes
│   │   │   └── Sync/
│   │   │       ├── InterpolationSystem.cs   # Interpolación de posición remota
│   │   │       ├── ExtrapolationSystem.cs   # Predicción cuando hay lag
│   │   │       └── ClientSidePrediction.cs  # Predicción del movimiento local
│   │   │
│   │   ├── Gameplay/                    ← Lógica del juego
│   │   │   ├── Player/
│   │   │   │   ├── PlayerController.cs      # Movimiento local con prediction
│   │   │   │   ├── PlayerNetworkSync.cs     # Envía/recibe estado de red
│   │   │   │   ├── PlayerVisuals.cs         # Color y skin únicos por jugador
│   │   │   │   └── PlayerData.cs            # ID, nombre, skin, score
│   │   │   ├── Monster/
│   │   │   │   ├── MonsterAI.cs             # Pathfinding + lógica de persecución
│   │   │   │   ├── MonsterNetworkSync.cs    # Solo el host mueve al monstruo
│   │   │   │   └── MonsterSenses.cs         # Detección de jugadores cercanos
│   │   │   ├── Collectibles/
│   │   │   │   ├── CollectibleItem.cs       # Item recolectable en el mapa
│   │   │   │   ├── CollectibleManager.cs    # Registro de items y progreso
│   │   │   │   └── ItemSpawner.cs           # Spawn distribuido en el mapa
│   │   │   └── GameRules.cs                 # Reglas modificables por el host
│   │   │
│   │   ├── Admin/                       ← Panel de administración (host)
│   │   │   ├── AdminController.cs           # Lógica de kick, ban, pausa
│   │   │   ├── KickSystem.cs                # Desconexión forzada de jugador
│   │   │   ├── BanSystem.cs                 # Baneo permanente de IP
│   │   │   └── PauseSystem.cs               # Pausa global sincronizada
│   │   │
│   │   └── UI/                          ← Interfaz de usuario
│   │       ├── Lobby/
│   │       │   ├── LobbyUI.cs               # Pantalla de espera con lista de jugadores
│   │       │   ├── HostSetupUI.cs           # UI para crear partida
│   │       │   └── JoinUI.cs                # UI para unirse (IP + puerto)
│   │       ├── HUD/
│   │       │   ├── HUDManager.cs            # HUD durante el juego
│   │       │   ├── PingDisplay.cs           # Muestra latencia de cada jugador
│   │       │   ├── CollectiblesProgressUI.cs# Barra de progreso de items
│   │       │   └── PlayerListUI.cs          # Lista con pings en tiempo real
│   │       ├── Admin/
│   │       │   ├── AdminPanelUI.cs          # Panel de host (kick/ban/pausa/reglas)
│   │       │   └── GameRulesUI.cs           # Modificar reglas en caliente
│   │       └── Results/
│   │           ├── GameOverUI.cs            # Pantalla de fin de partida
│   │           └── ResultsUI.cs             # Tabla de resultados + botón reiniciar
│   │
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   ├── Lobby.unity
│   │   ├── GameMap.unity                    # Mapa principal del juego
│   │   └── Results.unity
│   │
│   ├── Prefabs/
│   │   ├── Network/
│   │   │   ├── HostManager.prefab
│   │   │   └── ClientManager.prefab
│   │   ├── Players/
│   │   │   ├── LocalPlayer.prefab
│   │   │   └── RemotePlayer.prefab
│   │   ├── Monster/
│   │   │   └── SlendyMonster.prefab
│   │   └── Collectibles/
│   │       └── CollectibleItem.prefab
│   │
│   ├── Data/
│   │   ├── BanList.json                     # Archivo persistente de IPs baneadas
│   │   └── DefaultRules.json                # Reglas por defecto
│   │
│   └── Art/                             ← Assets visuales (placeholders al inicio)
│       ├── Players/                         # Sprites/materiales por color
│       ├── Monster/
│       ├── Map/
│       └── UI/
```

---

## 2. Arquitectura por capas

### Capa 1 — Presentación (UI Layer)
- Todo lo relacionado con la interfaz de usuario: pantallas, HUD, menús.
- **No contiene lógica de negocio.** Solo lee estado y dispara acciones.
- Archivos: `UI/`

### Capa 2 — Lógica de Juego (Gameplay Layer)
- Reglas del juego, entidades (jugadores, monstruo, ítems).
- El host ejecuta la simulación autoritativa; los clientes ejecutan predicción local.
- Archivos: `Gameplay/`

### Capa 3 — Red (Network Layer)
- Maneja toda la comunicación TCP/UDP, serialización, queues y sincronización.
- Completamente agnóstica del juego: no sabe qué significa un "ítem", solo serializa bytes.
- Archivos: `Network/`

### Capa 4 — Core
- GameManager (máquina de estados), constantes, configuraciones.
- Es la única capa que puede acceder a todas las demás.

---

## 3. Protocolo de red

### Canales
| Canal | Protocolo | Uso |
|-------|-----------|-----|
| Control | TCP | Conexión, kick, ban, chat, cambios de estado del juego |
| Estado del juego | UDP | Posiciones de jugadores, monstruo (alta frecuencia) |
| Eventos | TCP | Recoger ítem, muerte, pausa, fin de partida |

### OpCodes principales (NetworkProtocol.cs)
```
CONNECT_REQUEST      // Cliente solicita unirse
CONNECT_ACCEPT       // Host acepta + asigna ID
CONNECT_REJECT       // IP baneada o partida llena
PLAYER_INPUT         // Cliente → Host: inputs del frame
WORLD_STATE          // Host → Clientes: estado completo (UDP)
PLAYER_JOINED        // Nuevo jugador conectado
PLAYER_LEFT          // Jugador desconectado
ITEM_COLLECTED       // Ítem recolectado
GAME_STATE_CHANGE    // Lobby → Playing → GameOver
PING_REQUEST         // Medición de latencia (RTT)
PING_RESPONSE
KICK_PLAYER          // Host expulsa jugador
BAN_IP               // Host banea IP
PAUSE_GAME           // Host pausa/reanuda
UPDATE_RULES         // Host actualiza reglas
MONSTER_STATE        // Posición y estado del monstruo (UDP)
```

---

## 4. Sistema de sincronización

### Técnica: Interpolación + Client-Side Prediction

**Para jugadores remotos (lo que ves de otros):**
```
InterpolationSystem.cs
- Guarda un buffer circular de los últimos N estados recibidos
- Renderiza con ~100ms de delay respecto al estado actual
- Interpola suavemente entre snapshots → sin saltos visibles
```

**Para el jugador local (lo que tú controlas):**
```
ClientSidePrediction.cs
- Aplica el input localmente en el mismo frame (sin esperar al host)
- Guarda historial de inputs con timestamps
- Cuando llega la confirmación del host, reconcilia:
  · Si hay desfase → "rubber-band" suave de vuelta a la posición autoritativa
  · Si coincide → no hace nada (caso normal con bajo ping)
```

**Para el monstruo:**
```
MonsterNetworkSync.cs
- Solo el host calcula el pathfinding real
- Los clientes reciben posición + rotación del monstruo vía UDP
- Aplican interpolación igual que con jugadores remotos
```

---

## 5. Gestión de conexiones

### Conexión de un nuevo jugador
```
1. Cliente envía CONNECT_REQUEST (TCP) con nombre y versión
2. Host verifica: ¿IP en BanList? → CONNECT_REJECT
3. Host verifica: ¿Partida llena (max players)? → CONNECT_REJECT
4. Host asigna PlayerID + color único → CONNECT_ACCEPT
5. Host hace broadcast PLAYER_JOINED a todos los demás
6. Nuevo cliente recibe el estado actual del mundo (snapshot completo)
7. Nuevo cliente entra al juego sin interrumpir a los demás
```

### Desconexión de un jugador
```
1. Host detecta timeout TCP o recibe desconexión limpia
2. Host elimina al jugador del mundo (no bloquea la partida)
3. Host hace broadcast PLAYER_LEFT
4. Si quedan ≥ 1 jugador, el juego continúa normalmente
5. Si queda 0 jugadores: el host (si sigue activo) mantiene la sesión abierta
```

### Kick y Ban
```
KickSystem:   KICK_PLAYER → cierra conexión TCP → PLAYER_LEFT broadcast
BanSystem:    BAN_IP → guarda IP en BanList.json → cierra conexión → rechaza reconexión
```

---

## 6. Plan de desarrollo por fases

### FASE 1 — Infraestructura de red (Semana 1-2)
**Objetivo:** Dos instancias de Unity pueden conectarse entre sí.

- [ ] Implementar `HostNetworkManager`: listener TCP en puerto configurable
- [ ] Implementar `ClientNetworkHandler`: conectar a IP:Puerto
- [ ] Implementar `MessageQueue` thread-safe (ConcurrentQueue)
- [ ] Implementar `PacketSerializer` con BinaryWriter/Reader
- [ ] Definir `NetworkProtocol` (OpCodes) y `NetworkMessage` base
- [ ] Implementar handshake: CONNECT_REQUEST / CONNECT_ACCEPT / CONNECT_REJECT
- [ ] Agregar canal UDP paralelo para estado del juego
- [ ] Test: host + 2 clientes conectados simultáneamente

**Criterio de éxito:** 3 instancias abiertas, se conectan, se ven en logs.

---

### FASE 2 — Lobby y gestión de sesión (Semana 2-3)
**Objetivo:** UI de lobby funcional, jugadores visibles antes de iniciar.

- [ ] Implementar `GameStateMachine` (Lobby → Playing → GameOver → Results → Lobby)
- [ ] Crear escena Lobby con lista de jugadores en tiempo real
- [ ] `HostSetupUI`: nombre de partida, máx jugadores, reglas iniciales
- [ ] `JoinUI`: ingresar IP y puerto para conectarse
- [ ] Broadcast de PLAYER_JOINED / PLAYER_LEFT al lobby
- [ ] Botón "Iniciar Partida" (solo host), envía GAME_STATE_CHANGE
- [ ] `BanList.cs` con persistencia en JSON
- [ ] `PingMonitor`: envía PING_REQUEST cada segundo, calcula RTT
- [ ] `PingDisplay`: muestra ping junto a cada jugador en lobby

**Criterio de éxito:** Lobby con jugadores apareciendo/desapareciendo, pings visibles, host puede iniciar.

---

### FASE 3 — Movimiento sincronizado (Semana 3-4)
**Objetivo:** Los jugadores se mueven y todos lo ven fluidamente.

- [ ] `PlayerController`: WASD/joystick con física local
- [ ] `ClientSidePrediction`: el input se aplica inmediatamente en el cliente
- [ ] `PlayerNetworkSync`: envía PLAYER_INPUT por UDP cada FixedUpdate
- [ ] Host procesa inputs, genera WORLD_STATE y hace broadcast UDP
- [ ] `InterpolationSystem`: buffer de snapshots + interpolación con delay de ~100ms
- [ ] `PlayerVisuals`: asignar color/skin único por PlayerID al conectarse
- [ ] Test con latencia simulada (añadir delay artificial para probar interpolación)

**Criterio de éxito:** 3 jugadores moviéndose sin saltos visibles en 50ms de latencia simulada.

---

### FASE 4 — Gameplay: monstruo e ítems (Semana 4-5)
**Objetivo:** El juego tiene mecánicas jugables.

- [ ] Crear mapa: terreno, obstáculos, zonas de spawn para ítems
- [ ] `CollectibleItem` + `CollectibleManager`: N ítems en posiciones aleatorias
- [ ] `ItemSpawner`: distribuye ítems al inicio, sincroniza con clientes
- [ ] `MonsterAI`: pathfinding con NavMesh, persigue al jugador más cercano
- [ ] `MonsterNetworkSync`: host calcula AI, clientes reciben estado por UDP
- [ ] Condición de victoria: todos los ítems recogidos → GAME_STATE_CHANGE(GameOver)
- [ ] Condición de derrota: el monstruo atrapa a todos los jugadores → GameOver
- [ ] `GameRules.cs`: velocidad del monstruo, número de ítems, tiempo límite

**Criterio de éxito:** Partida completa jugable, puede ganarse y perderse.

---

### FASE 5 — Panel de administración (Semana 5-6)
**Objetivo:** El host tiene control total de la sesión.

- [ ] `AdminPanelUI`: panel flotante visible solo para el host
- [ ] `KickSystem`: botón junto a cada jugador en la lista
- [ ] `BanSystem`: opción adicional al kick, con confirmación
- [ ] `PauseSystem`: pausa global sincronizada (broadcast PAUSE_GAME), bloquea inputs
- [ ] `GameRulesUI`: sliders/inputs para modificar reglas en caliente (mid-game)
- [ ] Broadcast de cambios de reglas a todos los clientes
- [ ] Lista de jugadores con ping en tiempo real durante el juego

**Criterio de éxito:** Host puede kick/ban jugadores, pausar, y cambiar velocidad del monstruo sin reiniciar.

---

### FASE 6 — Ciclo completo + UI final (Semana 6-7)
**Objetivo:** Experiencia pulida de inicio a fin.

- [ ] `GameOverUI`: pantalla de victoria/derrota con animación
- [ ] `ResultsUI`: tabla con jugadores, ítems recolectados, tiempo de supervivencia
- [ ] Botón "Jugar de nuevo" (host) → reset del mundo → broadcast → volver a Lobby
- [ ] Sonidos básicos: pasos, recolección de ítem, monstruo cercano, game over
- [ ] Efectos visuales mínimos: parpadeo al recoger ítem, vignette cuando el monstruo está cerca
- [ ] Pulir HUD: indicador de ítems restantes, contador de jugadores vivos, ping
- [ ] Testing final con 3+ jugadores en red local

**Criterio de éxito:** Partida completa, reiniciable, sin errores críticos, UI coherente.

---

## 7. Detalles de implementación clave

### MessageQueue (thread-safe)
```csharp
// Network/Shared/MessageQueue.cs
public class MessageQueue {
    private ConcurrentQueue<NetworkMessage> _inbound  = new();
    private ConcurrentQueue<NetworkMessage> _outbound = new();

    // Llamado desde hilo de red
    public void EnqueueInbound(NetworkMessage msg)  => _inbound.Enqueue(msg);
    public void EnqueueOutbound(NetworkMessage msg) => _outbound.Enqueue(msg);

    // Llamado desde hilo principal de Unity (Update)
    public bool TryDequeueInbound(out NetworkMessage msg)  => _inbound.TryDequeue(out msg);
    public bool TryDequeueOutbound(out NetworkMessage msg) => _outbound.TryDequeue(out msg);
}
```

### Interpolación de posición
```csharp
// Network/Sync/InterpolationSystem.cs
// Guarda snapshots con timestamp; renderiza con interpolTime de delay
void Update() {
    float renderTime = Time.time - interpolationDelay; // ej: 0.1s
    // Encuentra los dos snapshots que rodean renderTime
    // Lerp entre ellos según t = (renderTime - from.time) / (to.time - from.time)
    transform.position = Vector3.Lerp(fromSnapshot.pos, toSnapshot.pos, t);
    transform.rotation = Quaternion.Slerp(fromSnapshot.rot, toSnapshot.rot, t);
}
```

### Client-Side Prediction + Reconciliación
```csharp
// 1. Aplica input localmente (mismo frame)
_localPosition += inputVector * speed * Time.deltaTime;

// 2. Envía input al host con sequenceNumber
SendInput(new InputMessage { seq = _seq++, input = inputVector });

// 3. Cuando llega confirmación del host con posición autoritativa:
float error = Vector3.Distance(_localPosition, authoritative.pos);
if (error > reconciliationThreshold) {
    // Reaplica todos los inputs pendientes desde el seq confirmado
    ReplayInputs(authoritative.pos, confirmedSeq);
}
```

---

## 8. Notas técnicas importantes

- **Unity no es thread-safe.** Toda la lógica de red corre en hilos separados. El `MessageQueue` es el único puente entre el hilo de red y el main thread de Unity. Nunca llames a `transform` o cualquier API de Unity desde un hilo de red.
- **UDP no garantiza orden.** Descarta paquetes WORLD_STATE con timestamp menor al último procesado.
- **El host es también cliente.** `GameManager` procesa los inputs del host localmente (sin red) pero los trata igual que los de cualquier otro jugador para consistencia.
- **Persistencia del BanList.** Usa `Application.persistentDataPath + "/banlist.json"` para que sobreviva reinicios del juego.
- **Prefabs de red separados.** `HostManager.prefab` y `ClientManager.prefab` se instancian según el rol elegido en el menú; nunca ambos al mismo tiempo.
- **Lag simulation en desarrollo.** Agrega un `NetworkLatencySimulator.cs` que añade delay artificial a los mensajes entrantes. Imprescindible para testear la interpolación correctamente en red local.
