# PropMapper

Property-Mapper für .NET. Einfach und grundlegend, aber **SEHR SCHNELL**.

Nur [eine CS-Datei](PropMapper.cs), ca. 193 Zeilen Code mit umfassender XML-Dokumentation.

## Was macht der PropMapper?

Der PropMapper kopiert automatisch Eigenschaften (Properties) von einem Objekt auf ein anderes. Er sucht dabei nach Eigenschaften mit gleichem Namen in Quell- und Zielobjekt und überträgt deren Werte – ohne dass man jeden Property-Zugriff manuell programmieren muss.

Beispiel: Man hat ein `Person`-Objekt aus der Datenbank und möchte dessen Daten in ein `PersonDTO`-Objekt übertragen, das an die UI weitergegeben wird. Der PropMapper erledigt das in einer Zeile.

**Intern** verwendet der PropMapper kompilierte Expression-Trees statt langsamer Reflection. Beim ersten Aufruf eines Typpaars (z. B. `Person` → `PersonDTO`) wird der Mapping-Code einmalig kompiliert und in einem statischen Cache gespeichert. Alle weiteren Aufrufe desselben Typpaars nutzen direkt den gecachten, kompilierten Code und sind daher extrem schnell.

## Installation

CS-Datei in das Projekt kopieren **oder** via [Nuget](https://www.nuget.org/packages/PropMapper/) installieren:

`Install-Package PropMapper`

(Dies fügt die `.cs`-Datei direkt in das Projekt ein – keine DLL-Abhängigkeit.)

## Verwendung

Die Bibliothek stellt Extension-Methoden über die statische Klasse `ClassClonator` bereit. Alle Methoden sind als Extension-Methoden auf jedem Objekt verfügbar:

### Neues Objekt erstellen (Klonen)

```cs
// Neues Objekt erstellen und alle passenden Properties kopieren
var destination = sourceObject.CreateCopy<SourceType, DestType>();
```

### In ein bestehendes Objekt kopieren

```cs
// Properties vom Quellobjekt in ein bestehendes Zielobjekt kopieren
sourceObject.CopyTo(destinationObject);

// Oder: CopyFrom am Zielobjekt aufrufen
destinationObject.CopyFrom(sourceObject);
```

### Auflistungen (Collections) kopieren

```cs
// Alle Objekte einer Auflistung kopieren
IEnumerable<DestType> destinations = sourceCollection.CopyAll<SourceType, DestType>();
```

## API-Referenz

### Extension-Methoden

Alle Methoden sind als Extension-Methoden auf jedem Objekt verfügbar:

#### `CreateCopy<TInput, TOutput>()`
Erstellt eine neue Instanz von `TOutput` und kopiert alle passenden Properties vom Eingabeobjekt.
- **Rückgabe**: Neue Instanz von `TOutput` mit kopierten Properties
- **Einschränkung**: `TOutput` muss einen parameterlosen Konstruktor besitzen
- **Wirft**: `ArgumentNullException`, wenn das Eingabeobjekt `null` ist

#### `CopyTo<TInput, TOutput>(TOutput output)`
Kopiert alle passenden Properties vom Eingabeobjekt in ein bestehendes Ausgabeobjekt.
- **Rückgabe**: `bool` – `true` bei Erfolg, `false` wenn Eingabe oder Ausgabe `null` ist

#### `CopyFrom<TInput, TOutput>(TInput input)`
Kopiert alle passenden Properties vom Eingabeobjekt in das aktuelle Objekt (Umkehrung von `CopyTo`).
- **Rückgabe**: `bool` – `true` bei Erfolg, `false` wenn Eingabe oder Ausgabe `null` ist

#### `CopyAll<TInput, TOutput>(IEnumerable<TInput>)`
Kopiert alle Objekte einer Auflistung in neue Instanzen.
- **Rückgabe**: `IEnumerable<TOutput>` – faul ausgewertete Folge kopierter Objekte
- **Einschränkung**: `TOutput` muss einen parameterlosen Konstruktor besitzen
- **Hinweis**: `null`-Einträge in der Eingabe werden übersprungen

## Wie funktioniert es intern?

PropMapper verwendet **kompilierte Expression-Trees** für optimale Performance:

1. **Statische Kompilierung**: Beim ersten Aufruf eines bestimmten Typpaars (z. B. `Person` → `Employee`) erstellt der Mapper kompilierte Expression-Trees in einem statischen Konstruktor.
2. **Caching**: Diese kompilierten Ausdrücke werden in statischen Variablen gespeichert, sodass nachfolgende Aufrufe extrem schnell sind.
3. **Property-Abgleich**: Properties werden nach Namen (Groß-/Kleinschreibung beachten) zwischen Quell- und Zieltyp abgeglichen.
4. **Typsicherheit**: Nur Properties mit übereinstimmenden Namen und kompatiblen Typen werden kopiert.

Dieser Ansatz bietet eine Performance nahe an manuell geschriebenem Code, mit dem Komfort automatischen Mappings.

## Benchmarks

Mapping eines einfachen Objekts mit 50 Properties, über 100.000 Iterationen:

| Mapper | Ergebnis |
|---|---|
| Automapper | 32.490 ms |
| Automapper mit gecachtem `config`-Objekt | 335 ms |
| **PropMapper** | **25 ms** |
| Manueller Code | 10 ms |

PropMapper ist mehr als 13-mal schneller als gecachter Automapper. Hier die getestete Klasse:

```cs
public class Tester
{
    public string prop1 { get; set; }
    public string prop2 { get; set; }
    public string prop3 { get; set; }
    public int iprop1 { get; set; }
    // usw., 50 Properties
}
```

## Verwendungsbeispiele

### Einfaches Objekt-Mapping

```cs
public class Person
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
}

public class PersonDTO
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
}

// Neues DTO aus einer Person erstellen
var person = new Person { FirstName = "Max", LastName = "Mustermann", Age = 30 };
var dto = person.CreateCopy<Person, PersonDTO>();
```

### Basisklasse in abgeleitete Klasse umwandeln

```cs
public class Person
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class Employee : Person
{
    public string Title { get; set; }

    public Employee(Person person)
    {
        // Alle Properties von Person in diese Employee-Instanz kopieren
        person.CopyTo(this);
    }
}
```

### Auflistungen mappen

```cs
List<Person> people = GetPeople();
IEnumerable<PersonDTO> dtos = people.CopyAll<Person, PersonDTO>();

// Mit LINQ kombinieren
var adultDtos = people
    .Where(p => p.Age >= 18)
    .CopyAll<Person, PersonDTO>()
    .ToList();
```

### Bestehende Objekte aktualisieren

```cs
public void UpdatePerson(Person existingPerson, PersonDTO updatedData)
{
    // Properties vom DTO in die bestehende Entität kopieren
    updatedData.CopyTo(existingPerson);
    // oder
    existingPerson.CopyFrom(updatedData);
}
```

## Features

✅ **Schnell**: Verwendet kompilierte Ausdrücke statt Reflection  
✅ **Einfach**: Einzelne C#-Datei, keine Abhängigkeiten  
✅ **Typsicher**: Generische Methoden mit Typprüfung zur Kompilierzeit  
✅ **Null-sicher**: Eingebaute Null-Prüfung  
✅ **Flexibel**: Funktioniert mit beliebigen POCO-Objekten  
✅ **Collection-Unterstützung**: Massen-Kopieroperationen mit LINQ-Integration  
✅ **Extension-Methoden**: Natürliche, fließende API  

## Einschränkungen

- Es werden nur öffentliche Properties mit übereinstimmenden Namen kopiert
- Die Namensübereinstimmung unterscheidet Groß- und Kleinschreibung
- Die Quell-Property muss lesbar sein (`CanRead`)
- Die Ziel-Property muss schreibbar sein (`CanWrite`)
- Keine Unterstützung für verschachteltes Objekt-Mapping (nur flache Kopie)
- Keine benutzerdefinierte Mapping-Konfiguration
- Der Zieltyp muss einen parameterlosen Konstruktor besitzen (für `CreateCopy` und `CopyAll`)

## Performance-Hinweise

- **Erster Aufruf**: Der erste Aufruf für ein bestimmtes Typpaar hat einen kleinen Mehraufwand durch die Expression-Kompilierung
- **Folgeaufrufe**: Extrem schnell dank gecachter kompilierter Ausdrücke
- **Speicher**: Jedes eindeutige Typpaar erstellt einen statischen gecachten Ausdruck
- **Thread-sicher**: Statische Konstruktoren sind thread-sicher

## Namespace

Die Bibliothek befindet sich im Namespace `DX.Shared`. Folgendes `using` hinzufügen:

```cs
using DX.Shared;
```

## Kompatibilität

- .NET Standard 2.0+
- .NET Framework 4.6.1+
- .NET Core 2.0+
- .NET 5.0+
- .NET 6.0+
- Nullable Reference Types aktiviert

## Lizenz

MIT-Lizenz – Details in der [LICENSE](LICENSE)-Datei.

## Danksagung

Ursprünglich erstellt von [Jitbit](https://www.jitbit.com/). Das Tool ist produktiv im Einsatz und wird regelmäßig getestet.
