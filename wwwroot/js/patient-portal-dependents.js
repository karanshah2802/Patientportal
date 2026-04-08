/* Patient portal /Patient page — dependents grid + modal (handlers on /Appointment/Index). */
(function () {
    'use strict';

    window.validateAge = function (input) {
        input.value = input.value.replace(/[^0-9]/g, '');
        if (input.value.length > 2) {
            input.value = input.value.slice(0, 2);
        }
    };

    function ejsDropdownTextValue(elementId) {
        var el = document.getElementById(elementId);
        if (!el || !el.ej2_instances || !el.ej2_instances[0]) return "";
        var inst = el.ej2_instances[0];
        var v = inst.value;
        if (v != null && v !== "") return String(v).trim();
        var tx = inst.text;
        if (tx != null && tx !== "") {
            var ph = inst.placeholder;
            if (!ph || String(tx) !== String(ph)) return String(tx).trim();
        }
        return "";
    }

    function ejsDropdownNumericValue(elementId) {
        var el = document.getElementById(elementId);
        if (!el || !el.ej2_instances || !el.ej2_instances[0]) return null;
        var inst = el.ej2_instances[0];
        var v = inst.value;
        if (v != null && v !== "") {
            var n = Number(v);
            if (!isNaN(n)) return n;
        }
        var item = inst.itemData;
        if (item && item.Id != null && item.Id !== "") {
            var n2 = Number(item.Id);
            if (!isNaN(n2)) return n2;
        }
        return null;
    }

    function setEjsDropdownValue(elementId, value) {
        var el = document.getElementById(elementId);
        if (!el || !el.ej2_instances || !el.ej2_instances[0]) return;
        el.ej2_instances[0].value = value !== undefined && value !== null && value !== "" ? value : null;
        el.ej2_instances[0].dataBind();
    }

    function setDepModalRelationByName(nameStr) {
        var el = document.getElementById("dep-modal-relation");
        if (!el || !el.ej2_instances || !el.ej2_instances[0] || !nameStr) return;
        var inst = el.ej2_instances[0];
        var ds = inst.dataSource;
        if (!ds || !ds.length) return;
        var target = String(nameStr).trim().toLowerCase();
        for (var i = 0; i < ds.length; i++) {
            var row = ds[i];
            var nm = row.Name != null ? String(row.Name).trim().toLowerCase() : "";
            if (nm === target) {
                inst.value = row.Id;
                inst.dataBind();
                return;
            }
        }
    }

    function getDepModalRelationName() {
        var el = document.getElementById("dep-modal-relation");
        if (!el || !el.ej2_instances || !el.ej2_instances[0]) return "";
        var inst = el.ej2_instances[0];
        var v = inst.value;
        if (v == null || v === "") return "";
        var ds = inst.dataSource;
        if (!ds || !ds.length) return "";
        for (var i = 0; i < ds.length; i++) {
            var row = ds[i];
            if (Number(row.Id) === Number(v)) return row.Name != null ? String(row.Name) : "";
        }
        return "";
    }

    function clearDependentModalValidationMessages() {
        var $ = window.jQuery;
        if (!$) return;
        $("#dep-modal-namespan").text("");
        $("#dep-modal-agespan").text("");
        $("#dep-modal-genderspan").text("");
        $("#dep-modal-relationspan").text("");
    }

    function clearDependentModalForAdd() {
        var $ = window.jQuery;
        if (!$) return;
        $("#dep-modal-editing-profile-id").val("");
        $("#dep-modal-name").val("");
        $("#dep-modal-age").val("");
        clearDependentModalValidationMessages();
        setEjsDropdownValue("dep-modal-gender", null);
        setEjsDropdownValue("dep-modal-relation", null);
        var t = document.getElementById("addDependentAppointmentModalLabel");
        if (t) t.textContent = "Add Dependent Patient";
    }

    function fillDependentModalFromGridRow(d) {
        var $ = window.jQuery;
        if (!$) return;
        clearDependentModalValidationMessages();
        var id = d.id != null ? d.id : d.Id;
        $("#dep-modal-editing-profile-id").val(id != null && id !== "" ? String(id) : "");
        var nm = d.name != null ? d.name : d.Name;
        var ag = d.age != null ? d.age : d.Age;
        var gen = d.gender != null ? d.gender : d.Gender;
        $("#dep-modal-name").val(nm != null ? String(nm) : "");
        $("#dep-modal-age").val(ag != null && ag !== "" ? String(ag) : "");
        var gNorm = gen != null ? String(gen).trim() : "";
        if (gNorm) {
            var gl = gNorm.toLowerCase();
            if (gl === "female") setEjsDropdownValue("dep-modal-gender", "Female");
            else if (gl === "male") setEjsDropdownValue("dep-modal-gender", "Male");
            else setEjsDropdownValue("dep-modal-gender", "Other");
        } else setEjsDropdownValue("dep-modal-gender", null);
        var relPick = d.patientRelationshipName != null ? d.patientRelationshipName : (d.PatientRelationshipName != null ? d.PatientRelationshipName : "");
        if (relPick) setDepModalRelationByName(String(relPick).trim());
        else setEjsDropdownValue("dep-modal-relation", null);
        var t = document.getElementById("addDependentAppointmentModalLabel");
        if (t) t.textContent = "Edit Dependent Patient";
    }

    function validateDependentModalFields() {
        var $ = window.jQuery;
        if (!$) return false;
        clearDependentModalValidationMessages();
        var ok = true;
        var name = ($("#dep-modal-name").val() || "").trim();
        if (!name) {
            $("#dep-modal-namespan").text("Name is required.");
            ok = false;
        }
        var ageV = ($("#dep-modal-age").val() || "").trim();
        if (!ageV) {
            $("#dep-modal-agespan").text("Age is required.");
            ok = false;
        } else {
            var an = parseInt(ageV, 10);
            if (isNaN(an) || an < 1 || an > 99) {
                $("#dep-modal-agespan").text("Enter a valid age (1–99).");
                ok = false;
            }
        }
        var g = ejsDropdownTextValue("dep-modal-gender");
        if (!g) {
            $("#dep-modal-genderspan").text("Gender is required.");
            ok = false;
        }
        var relId = ejsDropdownNumericValue("dep-modal-relation");
        if (relId == null || relId <= 0) {
            var relByName = getDepModalRelationName();
            if (!(relByName && String(relByName).trim())) {
                $("#dep-modal-relationspan").text("Relation is required.");
                ok = false;
            }
        }
        return ok;
    }

    window.ejsDropdownTextValue = ejsDropdownTextValue;
    window.ejsDropdownNumericValue = ejsDropdownNumericValue;
    window.setEjsDropdownValue = setEjsDropdownValue;
    window.setDepModalRelationByName = setDepModalRelationByName;
    window.getDepModalRelationName = getDepModalRelationName;
    window.clearDependentModalValidationMessages = clearDependentModalValidationMessages;
    window.clearDependentModalForAdd = clearDependentModalForAdd;
    window.fillDependentModalFromGridRow = fillDependentModalFromGridRow;
    window.validateDependentModalFields = validateDependentModalFields;

    window.onPortalDependentsGridCreated = function (args) {
        var u = typeof window.__portalDependentsDmUrl === "string" ? window.__portalDependentsDmUrl : "";
        if (!u || u.indexOf("profileId=0") >= 0) return;
        try {
            var grid = (this && this.dataSource) ? this : (args && args.dataSource ? args : null);
            if (grid && grid.dataSource && typeof grid.dataSource.url === "string")
                grid.dataSource.url = u;
        } catch (e) { /* non-fatal */ }
    };

    window.openPortalDependentEditFromRow = function (d) {
        if (!d) return;
        var $ = window.jQuery;
        var id = d.id != null ? d.id : d.Id;
        var em = d.emails != null ? d.emails : d.Emails;
        if (id != null && id !== "") {
            if ($) $("#PatientId").val(String(id));
            var af = document.getElementById("pi-appointment-for");
            if (af && af.ej2_instances && af.ej2_instances[0]) {
                af.ej2_instances[0].value = Number(id);
                af.ej2_instances[0].dataBind();
            }
        }
        if ($ && em) {
            var $em = $("#pi-email");
            if ($em.length) $em.val(em);
        }
        if (typeof fillDependentModalFromGridRow === "function") {
            fillDependentModalFromGridRow(d);
        }
        var mEl = document.getElementById("addDependentAppointmentModal");
        if (mEl && typeof bootstrap !== "undefined") {
            bootstrap.Modal.getOrCreateInstance(mEl).show();
        }
    };

    window.onPortalDependentRecordDoubleClick = function (args) {
        if (!args || !args.rowData) return;
        window.openPortalDependentEditFromRow(args.rowData);
    };

    window.onPortalDependentsDataBound = function (args) {
        var gridEl = document.getElementById("GridPortalDependents");
        if (!gridEl || !gridEl.ej2_instances || !gridEl.ej2_instances[0]) return;
        var grid = gridEl.ej2_instances[0];
        var root = grid.element;
        if (!root || typeof grid.getRowInfo !== "function") return;
        var btns = root.querySelectorAll(".portal-dep-edit-btn");
        for (var i = 0; i < btns.length; i++) {
            btns[i].onclick = function (e) {
                e.preventDefault();
                e.stopPropagation();
                var tr = e.currentTarget.closest(".e-row");
                if (!tr) tr = e.currentTarget.closest("tr");
                if (!tr) return;
                var info = grid.getRowInfo(tr);
                if (info && info.rowData) {
                    window.openPortalDependentEditFromRow(info.rowData);
                }
            };
        }
    };

    window.loadPortalDependentsMobileCards = function () {
        var $ = window.jQuery;
        if (!$) return;
        var $c = $("#portal-dependents-container");
        if (!$c.length) return;
        var cardUrl = typeof window.__portalDependentsCardUrl === "string" ? window.__portalDependentsCardUrl : "";
        if (!cardUrl || cardUrl.indexOf("profileId=0") >= 0) {
            $c.empty();
            return;
        }
        $.ajax({
            url: cardUrl,
            type: "POST",
            contentType: "application/json",
            data: "{}",
            success: function (response) {
                var rows = response && response.result ? response.result : [];
                $c.empty();
                if (!rows.length) {
                    $c.append($("<p/>", { class: "text-muted small mb-0 portal-dep-mobile-empty", text: "No dependent patients added yet." }));
                    return;
                }
                $.each(rows, function (i, row) {
                    var name = row.name != null ? row.name : row.Name;
                    var age = row.age != null ? row.age : row.Age;
                    var gender = row.gender != null ? row.gender : row.Gender;
                    var rel = row.patientRelationshipName != null ? row.patientRelationshipName : row.PatientRelationshipName;
                    var ageStr = age != null && age !== "" ? String(age) : "";
                    var genderStr = gender != null && gender !== "" ? String(gender) : "";
                    var relStr = rel != null && rel !== "" ? String(rel) : "";
                    var $card = $("<div/>", { class: "ius-gridrow portal-dep-mobile-card" });
                    var $body = $("<div/>", { class: "ius-appbody" });
                    var $info = $("<div/>", { class: "ius-appinfo" });
                    $info.append($("<h3/>", { text: name != null && name !== "" ? String(name) : "—" }));
                    var $tyfo = $("<div/>", { class: "ius-apptyfo" });
                    var typeParts = [];
                    if (ageStr) typeParts.push("Age " + ageStr);
                    if (genderStr) typeParts.push(genderStr);
                    if (typeParts.length) {
                        $tyfo.append($("<span/>", { class: "appinfo-type", text: typeParts[0] }));
                        for (var p = 1; p < typeParts.length; p++) {
                            $tyfo.append($("<span/>", { class: "appinfo-dot", "aria-hidden": "true" }));
                            $tyfo.append($("<span/>", { class: "appinfo-form", text: typeParts[p] }));
                        }
                    } else {
                        $tyfo.append($("<span/>", { class: "appinfo-type text-muted", text: "—" }));
                    }
                    $info.append($tyfo);
                    var $ctrls = $("<div/>", { class: "ius-ctrls" });
                    var $edit = $("<button/>", {
                        type: "button",
                        class: "portal-dep-mobile-edit-icon",
                        "aria-label": "Edit dependent",
                        html: '<i class="fa-solid fa-pen-to-square" aria-hidden="true"></i>'
                    });
                    $edit.on("click", function () { window.openPortalDependentEditFromRow(row); });
                    $ctrls.append($edit);
                    $body.append($info).append($ctrls);
                    var $foo = $("<div/>", { class: "ius-foo" });
                    var $dt = $("<div/>", { class: "ius-datetime" });
                    $dt.append($("<div/>", { class: "datetime-label text-sb", text: "Relation" }));
                    $dt.append($("<div/>", { text: relStr || "—" }));
                    var $st = $("<div/>", { class: "ius-status" });
                    $st.append($("<div/>", { class: "ius-badge badge-green", text: "Dependent" }));
                    $foo.append($dt).append($st);
                    $card.append($body).append($foo);
                    $c.append($card);
                });
            },
            error: function () {
                $c.empty().append($("<p/>", { class: "text-danger small mb-0 px-1", text: "Unable to load dependents." }));
            }
        });
    };

    window.refreshPortalDependentsGrid = function () {
        function touchMobileCards() {
            if (typeof window.loadPortalDependentsMobileCards === "function")
                window.loadPortalDependentsMobileCards();
        }
        var gridEl = document.getElementById("GridPortalDependents");
        if (!gridEl || !gridEl.ej2_instances || !gridEl.ej2_instances[0]) {
            touchMobileCards();
            return;
        }
        var grid = gridEl.ej2_instances[0];
        var dataNs = (typeof ej !== "undefined" && ej.data) ? ej.data
            : ((typeof ej2 !== "undefined" && ej2.data) ? ej2.data : null);

        function spin(on) {
            try {
                if (on && grid.showSpinner) grid.showSpinner();
                else if (!on && grid.hideSpinner) grid.hideSpinner();
            } catch (x) { }
        }

        try {
            var ds = grid.dataSource;
            if (ds && typeof ds.executeQuery === "function" && dataNs && dataNs.Query) {
                spin(true);
                ds.executeQuery(new dataNs.Query()).then(function () {
                    spin(false);
                    try { grid.refresh(); } catch (r) { }
                    touchMobileCards();
                }).catch(function () {
                    spin(false);
                    try { grid.refresh(); } catch (r2) { }
                    touchMobileCards();
                });
                return;
            }
        } catch (e) { }

        try {
            var u = typeof window.__portalDependentsDmUrl === "string" ? window.__portalDependentsDmUrl : "";
            if (dataNs && dataNs.DataManager && dataNs.UrlAdaptor && u && u.indexOf("profileId=0") < 0) {
                grid.dataSource = new dataNs.DataManager({
                    url: u,
                    adaptor: new dataNs.UrlAdaptor(),
                    crossDomain: true
                });
            }
        } catch (e2) { }

        try { grid.refresh(); } catch (r3) { }
        touchMobileCards();
    };

    jQuery(function () {
        if (!document.getElementById("GridPortalDependents") && !document.getElementById("portal-dependents-container")) return;

        var depModalEl = document.getElementById("addDependentAppointmentModal");
        if (depModalEl) {
            depModalEl.addEventListener("show.bs.modal", function (ev) {
                if (typeof clearDependentModalValidationMessages === "function") {
                    clearDependentModalValidationMessages();
                }
                var trigger = ev.relatedTarget;
                if (trigger && trigger.id === "open-dependent-details-btn" && typeof clearDependentModalForAdd === "function") {
                    clearDependentModalForAdd();
                }
            });
            depModalEl.addEventListener("hidden.bs.modal", function () {
                if (typeof clearDependentModalValidationMessages === "function") {
                    clearDependentModalValidationMessages();
                }
                if (typeof window.cleanupAppointmentModalBackdrop === "function") {
                    window.cleanupAppointmentModalBackdrop();
                }
            });
        }

        $("#dependent-modal-done-btn").off("click.patientPortalDepSave").on("click.patientPortalDepSave", function () {
            if (typeof validateDependentModalFields === "function" && !validateDependentModalFields()) return;
            var $btn = $(this);
            var editRaw = ($("#dep-modal-editing-profile-id").val() || "").trim();
            var editId = editRaw ? parseInt(editRaw, 10) : 0;
            var upsertDependentId = (editId > 0 && !isNaN(editId)) ? editId : null;
            var payload = {
                name: ($("#dep-modal-name").val() || "").trim(),
                age: parseInt(($("#dep-modal-age").val() || "").trim(), 10),
                patientRelationShipId: typeof ejsDropdownNumericValue === "function" ? ejsDropdownNumericValue("dep-modal-relation") : null,
                gender: typeof ejsDropdownTextValue === "function" ? ejsDropdownTextValue("dep-modal-gender") : "",
                dependentProfileId: upsertDependentId
            };
            var fallbackDependentProfileId = upsertDependentId;
            $btn.prop("disabled", true).text("Saving...");
            $.ajax({
                url: "/Appointment?handler=UpsertPortalDependent",
                type: "POST",
                dataType: "json",
                contentType: "application/json",
                data: JSON.stringify(payload),
                success: function (res) {
                    $btn.prop("disabled", false).text("Save");
                    var ok = res && (res.success === true || res.Success === true);
                    var msg = (res && (res.message || res.Message)) || "";
                    function notifySuccess(text) {
                        if (typeof toastr !== "undefined") {
                            toastr.success(text || "Dependent saved.");
                        } else {
                            alert(text || "Dependent saved.");
                        }
                    }
                    function notifyError(text) {
                        if (typeof toastr !== "undefined") {
                            toastr.error(text || "Could not save dependent.");
                        } else {
                            alert(text || "Could not save dependent.");
                        }
                    }
                    function hideDependentModal() {
                        var mEl = document.getElementById("addDependentAppointmentModal");
                        if (!mEl || typeof bootstrap === "undefined") return;
                        var mi = bootstrap.Modal.getInstance(mEl);
                        if (!mi) mi = bootstrap.Modal.getOrCreateInstance(mEl);
                        mi.hide();
                        if (typeof window.cleanupAppointmentModalBackdrop === "function") {
                            window.cleanupAppointmentModalBackdrop();
                        }
                    }
                    if (ok) {
                        notifySuccess(msg || "Dependent saved.");
                        hideDependentModal();
                        try {
                            var newId = res.id != null ? res.id : res.Id;
                            var nid = newId != null && newId !== "" ? Number(newId) : 0;
                            if (!nid || isNaN(nid) || nid <= 0) {
                                nid = fallbackDependentProfileId != null ? Number(fallbackDependentProfileId) : 0;
                            }
                            if (nid > 0 && !isNaN(nid)) {
                                $("#PatientId").val(String(nid));
                            }
                        } catch (e) { /* ignore */ }
                        setTimeout(function () {
                            if (typeof window.refreshPortalDependentsGrid === "function") {
                                window.refreshPortalDependentsGrid();
                            }
                        }, 400);
                    } else {
                        notifyError(msg || "Could not save dependent.");
                    }
                },
                error: function (xhr) {
                    $btn.prop("disabled", false).text("Save");
                    var errText = "Could not save dependent.";
                    try {
                        var j = typeof xhr.responseJSON === "object" && xhr.responseJSON !== null
                            ? xhr.responseJSON
                            : JSON.parse(xhr.responseText || "{}");
                        if (j && (j.message || j.Message)) errText = j.message || j.Message;
                    } catch (e) { /* ignore */ }
                    if (typeof toastr !== "undefined") toastr.error(errText);
                    else alert(errText);
                }
            });
        });

        if (typeof window.loadPortalDependentsMobileCards === "function") {
            window.loadPortalDependentsMobileCards();
        }
    });
})();
