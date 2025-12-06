# ContainerRegistryService - Guida all'Uso

## Panoramica

Il `ContainerRegistryService` fornisce accesso al registro dei container (casse) memorizzati in Valkey. È ottimizzato per ricerche autocomplete sul campo `cassa`.

## Iniezione del Service

```java
@Autowired
private ContainerRegistryService containerRegistryService;
```

Oppure tramite costruttore:

```java
private final ContainerRegistryService containerRegistryService;

public MyController(ContainerRegistryService containerRegistryService) {
    this.containerRegistryService = containerRegistryService;
}
```

## Metodi Disponibili

### 1. searchByPrefix - Ricerca per Prefisso (Autocomplete)

Ricerca efficiente per prefisso. Ideale per dropdown con autocomplete.

```java
// Trova container il cui codice cassa inizia con "GBTU"
List<ContainerDTO> results = containerRegistryService.searchByPrefix("GBTU", 20);

// Equivalente SQL: WHERE UPPER(cassa) LIKE 'GBTU%' LIMIT 20
```

**Parametri:**
- `prefix` - Il prefisso da cercare (case-insensitive)
- `limit` - Numero massimo di risultati (default 20 se <= 0)

**Performance:** O(log(N) + M) dove N = totale container, M = risultati

### 2. search - Ricerca LIKE (Contiene)

Ricerca container che contengono il pattern in qualsiasi posizione.

```java
// Trova container il cui codice cassa contiene "028"
List<ContainerDTO> results = containerRegistryService.search("028", 20);

// Equivalente SQL: WHERE UPPER(cassa) LIKE '%028%' LIMIT 20
```

**Parametri:**
- `pattern` - Il pattern da cercare (case-insensitive)
- `limit` - Numero massimo di risultati (default 20 se <= 0)

**Performance:** O(N) - scansiona tutto l'indice, usare con parsimonia

### 3. getById - Recupero per ID

Recupera un singolo container per ID.

```java
Optional<ContainerDTO> container = containerRegistryService.getById(2121);

if (container.isPresent()) {
    ContainerDTO c = container.get();
    System.out.println(c.getCassa()); // "GBTU 028130.1"
}
```

**Performance:** O(1)

### 4. count - Conteggio Totale

Conta il numero totale di container nell'indice.

```java
long totalContainers = containerRegistryService.count();
```

### 5. indexExists - Verifica Esistenza Indice

Verifica se l'indice dei container esiste in Valkey.

```java
if (!containerRegistryService.indexExists()) {
    logger.warn("Indice container non presente in Valkey");
}
```

## Esempio Completo: Endpoint Autocomplete

```java
@RestController
@RequestMapping("/api/containers")
public class ContainerController {

    private final ContainerRegistryService containerRegistryService;

    public ContainerController(ContainerRegistryService containerRegistryService) {
        this.containerRegistryService = containerRegistryService;
    }

    /**
     * Endpoint per autocomplete dropdown
     * GET /api/containers/search?q=GBTU&limit=10
     */
    @GetMapping("/search")
    public ResponseEntity<List<ContainerDTO>> search(
            @RequestParam String q,
            @RequestParam(defaultValue = "20") int limit) {

        List<ContainerDTO> results = containerRegistryService.search(q, limit);
        return ResponseEntity.ok(results);
    }

    /**
     * Endpoint per ricerca per prefisso (più veloce)
     * GET /api/containers/autocomplete?prefix=GBTU&limit=10
     */
    @GetMapping("/autocomplete")
    public ResponseEntity<List<ContainerDTO>> autocomplete(
            @RequestParam String prefix,
            @RequestParam(defaultValue = "20") int limit) {

        List<ContainerDTO> results = containerRegistryService.searchByPrefix(prefix, limit);
        return ResponseEntity.ok(results);
    }

    /**
     * GET /api/containers/{id}
     */
    @GetMapping("/{id}")
    public ResponseEntity<ContainerDTO> getById(@PathVariable Integer id) {
        return containerRegistryService.getById(id)
                .map(ResponseEntity::ok)
                .orElse(ResponseEntity.notFound().build());
    }

    /**
     * GET /api/containers/count
     */
    @GetMapping("/count")
    public ResponseEntity<Long> count() {
        return ResponseEntity.ok(containerRegistryService.count());
    }
}
```

## Struttura ContainerDTO

```java
public class ContainerDTO {
    private Integer id;
    private String cassa;           // Codice cassa (es. "GBTU 028130.1")
    private String descrizione;     // Descrizione testuale
    private Integer piantoni;
    private String tipo;
    private String nota;
    private Boolean container;
    private Boolean mobile;
    private Boolean rottami;
    private Integer larghezza;
    private Integer altezza;
    private Integer lunghezza;
    private Integer volume;
    private String manutenzione;
    private String modello;
    private String numserie;
    private String controllock;
    private Integer portatakg;
    private Boolean sponda;
    private Boolean gru;
    private Boolean carrelli;
    private Boolean transpallet;
    private Boolean pesaAPonte;
    private String targa;
    private Boolean assali;
    private Boolean pneumatici;
    private Boolean ck;
    private String ckData;
    private Integer giorniPre;
    private Integer tara;
    private String identificativo;
    private String foto;
}
```

## Struttura Dati Valkey

I dati sono gestiti da un processo esterno con la seguente struttura:

| Chiave | Tipo | Descrizione |
|--------|------|-------------|
| `containers:index` | Sorted Set | Indice per ricerca. Membri: `"CASSA_CODE:ID"` (es. `"GBTU 028130.1:2121"`), score: 0 |
| `containers:data:{id}` | String | JSON del ContainerDTO |

## Best Practices

1. **Usa `searchByPrefix` per autocomplete** - È molto più efficiente di `search`
2. **Limita i risultati** - Non richiedere più di 50 risultati per volta
3. **Gestisci il caso vuoto** - I metodi restituiscono liste vuote, mai null
4. **Verifica l'indice all'avvio** - Usa `indexExists()` per verificare che i dati siano presenti
