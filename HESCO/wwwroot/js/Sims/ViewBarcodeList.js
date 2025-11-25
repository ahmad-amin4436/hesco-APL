// barcode Management JavaScript
$(document).ready(function () {
    // Initialize Select2 Dropdowns
    function initSelect2(selector, type) {
        $(selector).select2({
            placeholder: "Search " + type,
            allowClear: true,
            width: "100%",
            ajax: {
                url: barcodeUrls.getBarcodeData,
                type: "GET",
                dataType: "json",
                delay: 300,
                data: function (params) {
                    return {
                        suggestionType: type,
                        searchTerm: params.term
                    };
                },
                processResults: function (data) {
                    return {
                        results: data.map(x => ({
                            id: x.id || x.value || x.text,
                            text: x.text
                        }))
                    };
                },
                cache: true
            },
            minimumInputLength: 1
        });
    }

    const selectFilters = [
        { selector: '#OptocomCodeFilter', type: 'serialno' }
    ];

    selectFilters.forEach(f => initSelect2(f.selector, f.type));

    // Initialize Date Pickers with range selection
    function initDatePicker(selector) {
        $(selector).datepicker({
            startView: 0,
            minViewMode: 0,
            maxViewMode: 2,
            multidate: true,
            multidateSeparator: ",",
            autoClose: true,
            clearBtn: true,
            format: 'yyyy-mm-dd',
            beforeShowDay: function (date) {
                return highlightRange(date, selector);
            }
        }).on("changeDate", function (event) {
            var dates = event.dates, elem = $(selector);
            if (dates.length == 2) {
                reloadTable();
            }
            if (elem.data("selecteddates") == dates.join(",")) return;
            if (dates.length > 2) dates = dates.splice(dates.length - 1);
            dates.sort(function (a, b) { return new Date(a).getTime() - new Date(b).getTime() });
            elem.data("selecteddates", dates.join(",")).datepicker('setDates', dates);

        }).on("hide", function (event) {
            var dates = event.dates, elem = $(selector);
            if (dates.length == 1) {
                dates.push(dates[0]);
                dates.sort(function (a, b) { return new Date(a).getTime() - new Date(b).getTime() });
                elem.data("selecteddates", dates.join(",")).datepicker('setDates', dates);
                reloadTable();
            }
        });

        // Initialize with empty selected dates
        $(selector).data("selecteddates", "");
    }

    function highlightRange(date, selector) {
        var selectedDates = $(selector).datepicker('getDates');
        if (selectedDates.length === 2 && date >= selectedDates[0] && date <= selectedDates[1]) {
            return 'highlighted';
        }
        return '';
    }

    // Initialize all date pickers
    initDatePicker('#CreatedAtFilter');
    initDatePicker('#UpdatedAtFilter');
    initDatePicker('#ChangeProjectDateFilter');
    initDatePicker('#MapDateTimeFilter');

    // Add CSS for highlighted dates
    const style = document.createElement('style');
    style.textContent = `
        .highlighted {
            background-color: #007bff !important;
            color: white !important;
            border-radius: 50%;
        }
        .datepicker table tr td.highlighted:hover {
            background-color: #0056b3 !important;
        }
    `;
    document.head.appendChild(style);

    var collapsedGroups = {};

    // Initialize DataTable
    var table = $('#BarcodeTable').DataTable({
        processing: true,
        serverSide: true,
        ordering: false,
        ajax: {
            url: barcodeUrls.getBarcodeData,
            type: 'GET',
            data: function (d) {
                return {
                    draw: d.draw,
                    foptocomcode: $('#OptocomCodeFilter').val(),
                    fproject: $('#ProjectFilter').val() ? $('#ProjectFilter').val().join(',') : null, // Serialize array as CSV
                    fchangeProject: $('#ChangeProjectFilter').val(),
                    fuploadedBy: $('#CreatedByFilter').val(),
                    fupdatedBy: $('#UpdatedByFilter').val(),
                    fuploadedAt: $('#CreatedAtFilter').val(),
                    fupdatedAt: $('#UpdatedAtFilter').val(),
                    fmapDateTime: $('#MapDateTimeFilter').val(),
                    fchangeProjectDate: $('#ChangeProjectDateFilter').val(),
                    fpage: (d.start / d.length) + 1,
                    fpageSize: d.length
                };
            },
            dataSrc: function (json) {
                // Update record count
                if (json.recordsTotal) {
                    var start = (table.page() * table.page.len()) + 1;
                    var end = Math.min(json.recordsTotal, (table.page() + 1) * table.page.len());
                    $('#recordCount').text(`Showing ${start} to ${end} of ${json.recordsTotal} records`);
                }
                return json.data;
            }
        },
        rowGroup: {
            dataSrc: 'project_name',
            startRender: function (rows, group) {
                var hasFilter = hasAnyFilter();

                if (hasFilter) {
                    var count = rows.count();
                    var collapsed = !!collapsedGroups[group];
                    var icon = collapsed ? '+' : '-';

                    return $('<tr/>')
                        .append('<td colspan="11" class="group-row" style="cursor:pointer; font-weight:bold; background:#f1f3f5;">' +
                            '<span>Project: ' + (group || 'No Project') + ' (' + count + ' items)</span> ' +
                            '<button class="toggle-btn" style="border:none; background:#e0e0e0; color:#333; width:24px; height:24px; border-radius:50%; font-weight:bold; font-size:14px; cursor:pointer;" data-group="' + group + '">' + icon + '</button>' +
                            '</td>');
                }
                else {
                    // No filter => disable grouping
                    table.rowGroup().disable();
                }
            }
        },
        columns: [
            { data: 'serial_no' },
            {
                data: 'project_name',
                render: function (data) {
                    return data || '-';
                }
            },
            {
                data: 'change_project_name',
                render: function (data) {
                    return data || '-';
                }
            },
            {
                data: 'created_by_username',
                render: function (data, type, row) {
                    return data || row.uploaded_by || '-';
                }
            },
            {
                data: 'created_at',
                render: function (data) {
                    if (!data || data.startsWith("0001-01-01")) return '';
                    return new Date(data).toLocaleString('en-US', {
                        year: 'numeric',
                        month: 'short',
                        day: 'numeric',
                        hour: '2-digit',
                        minute: '2-digit'
                    });
                }
            },
            {
                data: 'map_datetime',
                render: function (data) {
                    if (!data || data.startsWith("0001-01-01")) return '';
                    return new Date(data).toLocaleString('en-US', {
                        year: 'numeric',
                        month: 'short',
                        day: 'numeric',
                        hour: '2-digit',
                        minute: '2-digit'
                    });
                }
            },
            {
                data: 'change_project_id_at',
                render: function (data) {
                    if (!data || data.startsWith("0001-01-01")) return '';
                    return new Date(data).toLocaleDateString('en-US', {
                        year: 'numeric',
                        month: 'short',
                        day: 'numeric'
                    });
                }
            },
            {
                data: null,
                orderable: false,
                render: function (data) {
                    return renderbarcodeActionButtons(data, table);
                }
            }
        ],
        pageLength: 50,
        order: [[5, 'desc']],
        dom: 'lrtip',
        lengthMenu: [[25, 50, 75, 100, 200, 500], [25, 50, 75, 100, 200, 500]],
        pagingType: "full_numbers",
        language: {
            paginate: {
                first: '<i class="fas fa-angle-double-left"></i>',
                previous: '<i class="fas fa-angle-left"></i>',
                next: '<i class="fas fa-angle-right"></i>',
                last: '<i class="fas fa-angle-double-right"></i>'
            },
            info: "Showing _START_ to _END_ of _TOTAL_ entries"
        },
        responsive: true,

        drawCallback: function (settings) {
            // Restore collapsed state
            table.rows().every(function () {
                var data = this.data();
                var node = $(this.node());
                if (collapsedGroups[data.project_name]) {
                    node.hide();
                }
            });

            // Event delegation for toggle button
            $('#BarcodeTable tbody').off('click', '.toggle-btn').on('click', '.toggle-btn', function () {
                var group = $(this).data('group');
                collapsedGroups[group] = !collapsedGroups[group];

                table.rows().every(function () {
                    var data = this.data();
                    var node = $(this.node());
                    if (data.project_name === group) {
                        if (collapsedGroups[group]) node.hide();
                        else node.show();
                    }
                });

                $(this).text(collapsedGroups[group] ? '+' : '-');
            });
        }

    });
    // Custom Export Handlers - SERVER SIDE
    $('#exportExcelAll').on('click', function () {
        exportToExcel('all');
    });

    $('#exportExcelCurrent').on('click', function () {
        exportToExcel('current');
    });

    $('#exportPdfAll').on('click', function () {
        exportToPDF('all');
    });

    $('#exportPdfCurrent').on('click', function () {
        exportToPDF('current');
    });
    function getCurrentPageParams() {
        // Get current page info from DataTable
        var table = $('#BarcodeTable').DataTable();
        var pageInfo = table.page.info();

        return {
            fpage: pageInfo.page + 1, // DataTables is 0-based, server is 1-based
            fpageSize: pageInfo.length
        };
    }
    function hasAnyFilter() {
        const filters = [
            $('#OptocomCodeFilter').val(),
            $('#ProjectFilter').val(),
            $('#ChangeProjectFilter').val(),
            $('#CreatedByFilter').val(),
            $('#UpdatedByFilter').val(),
            $('#CreatedAtFilter').val(),
            $('#UpdatedAtFilter').val(),
            $('#MapDateTimeFilter').val(),
            $('#ChangeProjectDateFilter').val()
        ];

        return filters.some(f => {
            if (f == null) return false;         // null or undefined
            if (Array.isArray(f) && f.length === 0) return false; // empty array
            if (typeof f === 'string' && f.trim() === '') return false; // empty string
            return true; // valid filter
        });
    }


    // Server-side export functions
    function exportToExcel(exportType) {
        var params = getFilterParams();
        params.exportType = exportType;

        // Add pagination parameters for current page export
        if (exportType === 'current') {
            var paginationParams = getCurrentPageParams();
            params = { ...params, ...paginationParams };
        }

        window.location.href = barcodeUrls.exportExcel + '?' + $.param(params);
    }

    function exportToPDF(exportType) {
        var params = getFilterParams();
        params.exportType = exportType;

        // Add pagination parameters for current page export
        if (exportType === 'current') {
            var paginationParams = getCurrentPageParams();
            params = { ...params, ...paginationParams };
        }

        window.location.href = barcodeUrls.exportPDF + '?' + $.param(params);
    }

    function getFilterParams() {
        return {
            foptocomcode: $('#OptocomCodeFilter').val(),
            fproject: $('#ProjectFilter').val(),
            fchangeProject: $('#ChangeProjectFilter').val(),
            fstatus: '',
            fuploadedBy: $('#CreatedByFilter').val(),
            fupdatedBy: $('#UpdatedByFilter').val(),
            fuploadedAt: $('#CreatedAtFilter').val(),
            fupdatedAt: $('#UpdatedAtFilter').val(),
            fmapDateTime: $('#MapDateTimeFilter').val(),
            fchangeProjectDate: $('#ChangeProjectDateFilter').val(),
        };
    }
    // Reload table with filters
    function reloadTable() {
        var hasFilter = [
            $('#OptocomCodeFilter').val(),
            $('#ProjectFilter').val(),
            $('#ChangeProjectFilter').val(),
           $('#CreatedByFilter').val(),
            $('#UpdatedByFilter').val(),
            $('#CreatedAtFilter').val(),
            $('#UpdatedAtFilter').val(),
            $('#MapDateTimeFilter').val(),
            $('#ChangeProjectDateFilter').val()
        ].some(val => val);

        if (hasFilter) {
            table.rowGroup().dataSrc('project_name').enable();
        } else {
            table.rowGroup().disable();
        }

        table.ajax.reload();
    }

    // Filter change events
    $('.filters select').on('change', reloadTable);
    $('.filters input').on('change', reloadTable);

    // Export buttons

    // Clear filters
    $('#clearFiltersBtn').on('click', function () {
        $('.filters select').val(null).trigger('change');
        $('.filters input').val('');
        reloadTable();
    });

    // Render action buttons for barcode
    function renderbarcodeActionButtons(data, table) {
        let id = data.id;
        let barcode = data.serial_no;

        return `
            <div class="btn-group btn-group-sm" role="group" aria-label="barcode Actions">
                <a href="javascript:void(0);" title="View" class="btn btn-outline-info view-barcode-btn" data-id="${id}">
                    <i class="fas fa-eye"></i>
                </a>
                <button class="btn btn-outline-danger delete-barcode-btn" data-id="${id}" title="Delete">
                    <i class="fas fa-trash-alt"></i>
                </button>
            </div>
        `;
    }

    // AJAX Event Handlers for barcode Action Buttons
    $(document).on('click', '.view-barcode-btn', function (e) {
        e.preventDefault();
        let id = $(this).data('id');
        viewbarcodeDetails(id);
    });

    $(document).on('click', '.delete-barcode-btn', function (e) {
        e.preventDefault();
        let id = $(this).data('id');
        deletebarcode(id);
    });

    // AJAX function to view Barcode details
    function viewbarcodeDetails(id) {
        $.ajax({
            url: barcodeUrls.getBarcodeDetails,
            type: 'GET',
            data: { id: id },
            beforeSend: function () {
                showLoading('Loading barcode details...');
            },
            success: function (response) {
                hideLoading();
                if (response.success) {
                    showbarcodeModal(response.data, 'View Barcode Details');
                } else {
                    showNotification(response.message || 'Error loading barcode details', 'error');
                }
            },
            error: function (xhr, status, error) {
                hideLoading();
                showNotification('Error loading barcode details: ' + error, 'error');
            }
        });
    }

    // AJAX function to delete barcode
    function deletebarcode(id) {
        if (!confirm('Are you sure you want to delete this barcode record?')) {
            return;
        }

        $.ajax({
            url: barcodeUrls.deleteBarcode,
            type: 'POST',
            data: { id: id },
            beforeSend: function () {
                showLoading('Deleting barcode...');
            },
            success: function (response) {
                hideLoading();
                if (response.success) {
                    showNotification('barcode deleted successfully!', 'success');
                    $('#BarcodeTable').DataTable().ajax.reload(null, false);
                } else {
                    showNotification(response.message || 'Delete failed', 'error');
                }
            },
            error: function (xhr, status, error) {
                hideLoading();
                showNotification('Error deleting barcode: ' + error, 'error');
            }
        });
    }

    // Show barcode details in modal
    function showbarcodeModal(data, title) {
        // Format dates
        const formatDate = (dateString) => {
            if (!dateString || dateString.startsWith("0001-01-01")) return '-';
            return new Date(dateString).toLocaleString('en-US', {
                year: 'numeric',
                month: 'short',
                day: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            });
        };

        const formatDateOnly = (dateString) => {
            if (!dateString || dateString.startsWith("0001-01-01")) return '-';
            return new Date(dateString).toLocaleDateString('en-US', {
                year: 'numeric',
                month: 'short',
                day: 'numeric'
            });
        };

        // Create modal HTML for barcode
        const modalHtml = `
            <div class="modal fade" id="barcodeModal" tabindex="-1" role="dialog">
                <div class="modal-dialog modal-lg" role="document">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">${title}</h5>
                        </div>
                        <div class="modal-body">
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label><strong>Optocom Number:</strong></label>
                                        <p class="form-control-plaintext">${data.serial_no || '-'}</p>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label><strong>Barcode Flag:</strong></label>
                                        <p class="form-control-plaintext">${data.barcode_flag || '-'}</p>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label><strong>Project:</strong></label>
                                        <p class="form-control-plaintext">${data.project_name || '-'}</p>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label><strong>Change Project:</strong></label>
                                        <p class="form-control-plaintext">${data.change_project_name || '-'}</p>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label><strong>Uploaded By:</strong></label>
                                        <p class="form-control-plaintext">${data.created_by_username || '-'}</p>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label><strong>Uploaded At:</strong></label>
                                        <p class="form-control-plaintext">${formatDate(data.created_at)}</p>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label><strong>Updated By:</strong></label>
                                        <p class="form-control-plaintext">${data.updated_by_username || '-'}</p>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label><strong>Updated At:</strong></label>
                                        <p class="form-control-plaintext">${formatDate(data.updated_at)}</p>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label><strong>Map DateTime:</strong></label>
                                        <p class="form-control-plaintext">${formatDate(data.map_datetime)}</p>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label><strong>Change Project Date:</strong></label>
                                        <p class="form-control-plaintext">${formatDateOnly(data.change_project_id_at)}</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remove existing modal if any
        $('#barcodeModal').remove();

        // Append and show modal
        $('body').append(modalHtml);
        $('#barcodeModal').modal('show');
    }

    // Loading indicator functions
    function showLoading(message = 'Loading...') {
        $('#loadingOverlay').remove();
        const loaderHtml = `
            <div id="loadingOverlay" style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.5); z-index: 9999; display: flex; justify-content: center; align-items: center;">
                <div class="bg-white p-4 rounded shadow-lg text-center">
                    <div class="spinner-border text-primary mb-2" role="status">
                        <span class="sr-only">Loading...</span>
                    </div>
                    <p class="mb-0">${message}</p>
                </div>
            </div>
        `;
        $('body').append(loaderHtml);
    }

    function hideLoading() {
        $('#loadingOverlay').remove();
    }

    // Notification function
    function showNotification(message, type = 'info') {
        $('.alert-notification').remove();
        const alertClass = type === 'success' ? 'alert-success' :
            type === 'error' ? 'alert-danger' :
                type === 'warning' ? 'alert-warning' : 'alert-info';

        const notificationHtml = `
            <div class="alert ${alertClass} alert-notification alert-dismissible fade show" role="alert"
                 style="position: fixed; top: 20px; right: 20px; z-index: 9999; min-width: 300px;">
                ${message}
            </div>
        `;

        $('body').append(notificationHtml);

        setTimeout(() => {
            $('.alert-notification').alert('close');
        }, 5000);
    }



    function getFilterParams() {
        return {
            foptocomcode: $('#OptocomCodeFilter').val(),
            fproject: $('#ProjectFilter').val(),
            fchangeProject: $('#ChangeProjectFilter').val(),
            fuploadedBy: $('#CreatedByFilter').val(),
            fupdatedBy: $('#UpdatedByFilter').val(),
            fuploadedAt: $('#CreatedAtFilter').val(),
            fupdatedAt: $('#UpdatedAtFilter').val(),
            fmapDateTime: $('#MapDateTimeFilter').val(),
            fchangeProjectDate: $('#ChangeProjectDateFilter').val()
        };
    }

    // Clear All Filters function
    $('#btnClearAllFilters').on('click', function () {
        clearAllFilters();
    });

    function clearAllFilters() {
        // Clear all Select2 dropdowns
        $('.filters select').each(function () {
            $(this).val(null).trigger('change');
        });

        // Clear all date inputs
        $('.filters input[type="text"]').each(function () {
            $(this).val('');
            $(this).data("selecteddates", "");
            $(this).data("rangecomplete", false);
        });

        // Reset datepickers
        $('#CreatedAtFilter').datepicker('clearDates');
        $('#UpdatedAtFilter').datepicker('clearDates');
        $('#MapDateTimeFilter').datepicker('clearDates');
        $('#ChangeProjectDateFilter').datepicker('clearDates');

        // Reset data attributes for datepickers
        $('#CreatedAtFilter').data("selecteddates", "").data("rangecomplete", false);
        $('#UpdatedAtFilter').data("selecteddates", "").data("rangecomplete", false);
        $('#MapDateTimeFilter').data("selecteddates", "").data("rangecomplete", false);
        $('#ChangeProjectDateFilter').data("selecteddates", "").data("rangecomplete", false);

        // Show notification
        showNotification('All filters have been cleared', 'success');

        // Reload the table to show all records
        setTimeout(function () {
            reloadTable();
        }, 500);
    }

    // Initialize Select2 Dropdowns

    function initBarcodeSearch_DDL(selector, type, isMultiple = false) {
        const select = $(selector);

        // Initialize Select2 only if not already initialized
        if (!select.hasClass('select2-hidden-accessible')) {
            select.select2({
                placeholder: isMultiple ? "" : "Select " + type,
                allowClear: true,
                width: "100%",
                multiple: isMultiple
            });
        }

        // Populate options via AJAX
        $.ajax({
            url: barcodeUrls.loadBarcodeSearchDDL,
            type: "GET",
            dataType: "json",
            data: { suggestionType: type },
            success: function (response) {
                if (response.success && response.data) {
                    select.empty(); // clear existing options

                    // Only for single-select, add a blank placeholder option
                    if (!isMultiple) {
                        select.append(new Option("", "", false, false));
                    }

                    // Populate options
                    response.data.forEach(item => {
                        select.append(new Option(item.text, item.id, false, false));
                    });

                    // For multi-select, clear selection (empty initially)
                    if (isMultiple) {
                        select.val(null).trigger('change');
                    }
                }
            },
            error: function () {
                console.error("Failed to load " + type + " data.");
            }
        });
    }
    const barcodeFilters = [
        { select: '#ProjectFilter', type: 'project', isMultiple: true },
        { select: '#ChangeProjectFilter', type: 'changeproject', isMultiple: false },
        { select: '#StatusFilter', type: 'status', isMultiple: false },
        { select: '#CreatedByFilter', type: 'uploadedby', isMultiple: false },
        { select: '#UpdatedByFilter', type: 'updatedby', isMultiple: false }
    ];

    // Initialize filters
    barcodeFilters.forEach(f => initBarcodeSearch_DDL(f.select, f.type, f.isMultiple));

});