// Mobile card layout for lists: copy each column's header text onto its cells (shown on small screens by site.css).
(function () {
    document.querySelectorAll('table.table-stack').forEach(function (table) {
        var labels = Array.prototype.map.call(table.querySelectorAll('thead th'), function (th) { return th.textContent.trim(); });
        table.querySelectorAll('tbody tr').forEach(function (row) {
            Array.prototype.forEach.call(row.children, function (cell, i) {
                if (labels[i] && !cell.hasAttribute('data-label')) cell.setAttribute('data-label', labels[i]);
            });
        });
    });
})();

// ---------------------------------------------------------------------------------------------------------------
// Thousand separators in amount boxes (every input with inputmode="decimal", unless it has data-plain).
// People see 1,450,000 as they type; the commas are removed just before the form is sent, so the server always
// receives a plain number. Scripts read amounts with sbNum(...) and write them with sbSet(...).
// ---------------------------------------------------------------------------------------------------------------
(function () {
    var SELECTOR = 'input[inputmode="decimal"]:not([data-plain])';

    function group(value) {
        var s = String(value == null ? '' : value).trim();
        if (s === '') return '';
        var negative = s.charAt(0) === '-';
        s = s.replace(/[^0-9.]/g, '');
        var parts = s.split('.');
        var whole = parts.shift().replace(/^0+(?=\d)/, '');
        var decimals = parts.length ? '.' + parts.join('') : '';
        return (negative ? '-' : '') + whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',') + decimals;
    }

    function format(input, keepCaret) {
        var old = input.value;
        var next = group(old);
        if (next === old) return;
        var caret = input.selectionStart || 0;
        var countBefore = old.slice(0, caret).replace(/[^0-9.-]/g, '').length;
        input.value = next;
        if (keepCaret && document.activeElement === input) {
            var i = 0, seen = 0;
            while (i < next.length && seen < countBefore) { if (/[0-9.-]/.test(next.charAt(i))) seen++; i++; }
            input.setSelectionRange(i, i);
        }
    }

    // Read an amount (an input, or a string such as "1,450,000") as a number; 0 when empty.
    window.sbNum = function (v) {
        var s = v && typeof v === 'object' && 'value' in v ? v.value : v;
        var n = parseFloat(String(s == null ? '' : s).replace(/,/g, ''));
        return isNaN(n) ? 0 : n;
    };
    // Put an amount into an input, formatted.
    window.sbSet = function (input, value) {
        if (!input) return;
        input.value = value === '' || value == null ? '' : group(value);
    };

    document.addEventListener('input', function (e) {
        if (e.target.matches && e.target.matches(SELECTOR)) format(e.target, true);
    });
    document.addEventListener('focusout', function (e) {
        if (e.target.matches && e.target.matches(SELECTOR)) format(e.target, false);
    });
    // Remove the commas before sending (capture phase: before validation and any other submit handlers).
    document.addEventListener('submit', function (e) {
        e.target.querySelectorAll(SELECTOR).forEach(function (i) { i.value = i.value.replace(/,/g, ''); });
    }, true);

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll(SELECTOR).forEach(function (i) { format(i, false); });
        // Client-side validation (number, min, max, range) ignores the commas too.
        if (window.jQuery && jQuery.validator) {
            var m = jQuery.validator.methods;
            ['number', 'range', 'min', 'max'].forEach(function (k) {
                var original = m[k];
                if (!original) return;
                m[k] = function (value, element, param) {
                    return original.call(this, typeof value === 'string' ? value.replace(/,/g, '') : value, element, param);
                };
            });
        }
    });
})();

// FormData for scripts that send a form themselves (previews): amounts without their thousand separators.
window.sbFormData = function (form) {
    var data = new FormData(form);
    form.querySelectorAll('input[inputmode="decimal"]:not([data-plain])').forEach(function (i) {
        if (i.name) data.set(i.name, i.value.replace(/,/g, ''));
    });
    return data;
};
