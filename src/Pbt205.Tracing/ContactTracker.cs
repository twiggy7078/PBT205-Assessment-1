namespace Pbt205.Tracing;

public sealed class ContactTracker()
{
    private readonly Dictionary<string, (int X, int Y)> _personPositions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(int X, int Y), HashSet<string>> _gridOccupancy = new();
    private readonly Dictionary<string, List<Contact>> _contactHistory = new(StringComparer.OrdinalIgnoreCase);

    public bool TryUpdatePosition(string person, int x, int y, DateTimeOffset movedAt,
        out IReadOnlyList<Contact> contactsMade)
    {
        var pos = (x, y);
        var contacts = new List<Contact>();
        
        // check if person staying still
        if (pos == _personPositions.GetValueOrDefault(person))
        {
            contactsMade = contacts;
            return false;
        }

        // remove person from previous square
        if (_personPositions.TryGetValue(person, out var oldPos))
        {
            if (_gridOccupancy.TryGetValue(oldPos, out var occupants))
            {
                occupants.Remove(person);
                if (occupants.Count == 0) _gridOccupancy.Remove(oldPos);
            }
        }

        // then place them on new one
        _personPositions[person] = pos;
        if (!_gridOccupancy.TryGetValue(pos, out var currentOccupants))
        {
            currentOccupants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _gridOccupancy[pos] = currentOccupants;
        }

        // record contacts with anyone already here
        foreach (var other in currentOccupants)
        {
            if (string.Equals(other, person, StringComparison.OrdinalIgnoreCase)) continue;

            var contactForPerson = new Contact(other, pos.Item1, pos.Item2, movedAt);
            var contactForOther = new Contact(person, pos.Item1, pos.Item2, movedAt);

            RecordContact(person, contactForPerson);
            RecordContact(other, contactForOther);

            contacts.Add(contactForPerson);
        }
        
        currentOccupants.Add(person);
        contactsMade = contacts;
        return true;
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