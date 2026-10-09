namespace Pbt205.Tracing;

public sealed class ContactTracker()
{
    // Client identity resolution state
    private readonly Dictionary<Guid, string> _assignedNames = new();

    
    private readonly Dictionary<string, (int X, int Y)> _personPositions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(int X, int Y), HashSet<string>> _gridOccupancy = new();
    private readonly Dictionary<string, List<Contact>> _contactHistory = new(StringComparer.OrdinalIgnoreCase);

    public bool TryUpdatePosition(
        Guid clientId, 
        string requestedPerson, 
        int x, 
        int y, 
        DateTimeOffset movedAt,
        out string resolvedName,
        out IReadOnlyList<Contact> contactsMade)
    {
        // check if clientId already recorded
        if (!_assignedNames.TryGetValue(clientId, out resolvedName!))
        { // otherwise get new name
            resolvedName = GetNextAvailableName(requestedPerson);
            _assignedNames[clientId] = resolvedName;
        }

        var pos = (x, y);
        var contacts = new List<Contact>();
        
        // check if person first exists
        if (_personPositions.TryGetValue(resolvedName, out var oldPos))
        {// then check if staying still
            if (pos == oldPos)
            {
                contactsMade = contacts;
                return false;
            }
            // remove person from previous square
            if (_gridOccupancy.TryGetValue(oldPos, out var occupants))
            {
                occupants.Remove(resolvedName);
                if (occupants.Count == 0) _gridOccupancy.Remove(oldPos);
            } 
            
        }

        // then place them on new one
        _personPositions[resolvedName] = pos;
        if (!_gridOccupancy.TryGetValue(pos, out var currentOccupants))
        {
            currentOccupants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _gridOccupancy[pos] = currentOccupants;
        }

        // record contacts with anyone already here
        foreach (var other in currentOccupants)
        {
            if (string.Equals(other, resolvedName, StringComparison.OrdinalIgnoreCase)) continue;

            var contactForPerson = new Contact(other, pos.Item1, pos.Item2, movedAt);
            var contactForOther = new Contact(resolvedName, pos.Item1, pos.Item2, movedAt);

            RecordContact(resolvedName, contactForPerson);
            RecordContact(other, contactForOther);

            contacts.Add(contactForPerson);
        }
        
        currentOccupants.Add(resolvedName);
        contactsMade = contacts;
        return true;
    }
    
    private string GetNextAvailableName(string baseName)
    {
        var takenNames = new HashSet<string>(_assignedNames.Values, StringComparer.OrdinalIgnoreCase);
        if (!takenNames.Contains(baseName))
            return baseName;

        int suffix = 2;
        while (takenNames.Contains($"{baseName}-{suffix}"))
        {
            suffix++;
        }

        return $"{baseName}-{suffix}";
    }
    
    public IReadOnlyList<Contact> GetContacts(string person)
    {
        return _contactHistory.TryGetValue(person, out var history) 
            ? history.OrderByDescending(c => c.MetAt).ToList() 
            : [];
    }

    private void RecordContact(string person, Contact contact)
    {
        if (!_contactHistory.TryGetValue(person, out var list))
        {
            list = [];
            _contactHistory[person] = list;
        }
        list.Add(contact);
    }

}