using System;

namespace Galleon
{
    // A legacy station or damage case can isolate the people it directly exercises.
    internal sealed class AcceptanceCompanyFixture : IDisposable
    {
        readonly ShipCompany[] companies;
        readonly bool[] wasEnabled;

        internal AcceptanceCompanyFixture(params NavalShip[] ships)
        {
            companies = new ShipCompany[ships.Length];
            wasEnabled = new bool[ships.Length];
            for (int i = 0; i < ships.Length; i++)
            {
                if (!ships[i] || !ships[i].Company) continue;
                companies[i] = ships[i].Company;
                wasEnabled[i] = companies[i].enabled;
                companies[i].enabled = false;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < companies.Length; i++)
                if (companies[i]) companies[i].enabled = wasEnabled[i];
        }
    }
}
