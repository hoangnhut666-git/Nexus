// Client-side cascade for the (static SSR) address form: when the province changes,
// repopulate the ward/commune select from the bundled administrative-unit dataset.
// Server-side validation still enforces that the chosen pair is valid.
//
// This uses document-level event delegation (attached once) so it keeps working across
// Blazor enhanced navigation - scripts embedded in a page are NOT re-executed on an
// enhanced page load, but a listener on the persistent document is.

let unitsPromise = null;

function loadUnits(url) {
    unitsPromise ??= fetch(url)
        .then(response => {
            if (!response.ok) {
                throw new Error(`The server responded with status ${response.status}.`);
            }
            return response.json();
        })
        .then(data => new Map(data.map(entry => [entry.province, entry.wards ?? []])));

    return unitsPromise;
}

function rebuildWardOptions(wardSelect, wards) {
    const placeholder = wards.length > 0
        ? '-- Select ward / commune --'
        : '-- Select a province first --';

    const previous = wardSelect.value;
    wardSelect.replaceChildren();

    const placeholderOption = document.createElement('option');
    placeholderOption.value = '';
    placeholderOption.textContent = placeholder;
    wardSelect.appendChild(placeholderOption);

    for (const ward of wards) {
        const option = document.createElement('option');
        option.value = ward;
        option.textContent = ward;
        wardSelect.appendChild(option);
    }

    wardSelect.value = wards.includes(previous) ? previous : '';
}

document.addEventListener('change', async event => {
    const provinceSelect = event.target;
    if (!(provinceSelect instanceof HTMLSelectElement) || !provinceSelect.matches('[data-vn-province]')) {
        return;
    }

    const form = provinceSelect.closest('form');
    const wardSelect = form?.querySelector('select[data-vn-ward]');
    const unitsUrl = provinceSelect.getAttribute('data-units-url');
    if (!wardSelect || !unitsUrl) {
        return;
    }

    try {
        const wardsByProvince = await loadUnits(unitsUrl);
        rebuildWardOptions(wardSelect, wardsByProvince.get(provinceSelect.value) ?? []);
    } catch (error) {
        console.error('Failed to load Vietnamese administrative units.', error);
    }
});
