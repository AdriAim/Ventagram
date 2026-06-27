(() => {
  let ubikaFlashMessage = "";

  document.addEventListener("DOMContentLoaded", async () => {
    wireReportModal();
    await loadApiPage();
  });

  async function loadApiPage() {
    const host = document.getElementById("api-page");
    if (!host) return;

    const response = await fetch(host.dataset.apiEndpoint, {
      headers: { "X-Requested-With": "fetch" }
    });

    host.innerHTML = await response.text();
    if (ubikaFlashMessage) {
      const banner = document.createElement("div");
      banner.className = "status-banner";
      banner.textContent = ubikaFlashMessage;
      host.prepend(banner);
      ubikaFlashMessage = "";
    }

    initMap();
    wireReportForm();
    wireCreateForm();
  }

  function wireReportModal() {
    const reportModal = document.getElementById("reportModal");
    if (!reportModal) return;

    reportModal.addEventListener("show.bs.modal", event => {
      const button = event.relatedTarget;
      if (!button) return;

      const publicationId = button.getAttribute("data-publication-id");
      const title = button.getAttribute("data-publication-title");
      const idInput = reportModal.querySelector('input[name="publicationId"]');
      const titleNode = reportModal.querySelector("#reportModalTitle");

      if (idInput) idInput.value = publicationId;
      if (titleNode) titleNode.textContent = `Denunciar: ${title}`;
    });
  }

  function wireReportForm() {
    const form = document.getElementById("reportForm");
    if (!form || form.dataset.bound === "true") return;

    form.dataset.bound = "true";
    form.addEventListener("submit", async event => {
      event.preventDefault();

      const payload = {
        publicationId: Number(form.querySelector('input[name="publicationId"]').value),
        reason: form.querySelector('input[name="reason"]:checked').value
      };

      const response = await fetch("/api/content/report", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Requested-With": "fetch"
        },
        body: JSON.stringify(payload)
      });

      const result = await response.json();
      ubikaFlashMessage = result.message || "La denuncia fue enviada.";

      const modal = bootstrap.Modal.getInstance(document.getElementById("reportModal"));
      modal?.hide();
      await loadApiPage();
    });
  }

  function initMap() {
    const map = document.getElementById("map");
    if (!map) return;

    const apiKey = map.dataset.maptilerKey;
    if (!apiKey) return;

    const markers = JSON.parse(map.dataset.markers || "[]");
    if (!markers.length) return;

    const existingScript = document.querySelector('script[data-maptiler-sdk="true"]');
    if (existingScript) {
      createMap(markers, apiKey);
      return;
    }

    const script = document.createElement("script");
    script.dataset.maptilerSdk = "true";
    script.src = "https://cdn.maptiler.com/maptiler-sdk-js/latest/maptiler-sdk.umd.min.js";
    script.onload = () => {
      if (!document.querySelector('link[data-maptiler-css="true"]')) {
        const link = document.createElement("link");
        link.dataset.maptilerCss = "true";
        link.rel = "stylesheet";
        link.href = "https://cdn.maptiler.com/maptiler-sdk-js/latest/maptiler-sdk.css";
        document.head.appendChild(link);
      }

      createMap(markers, apiKey);
    };

    document.head.appendChild(script);
  }

  function createMap(markers, apiKey) {
    const map = document.getElementById("map");
    if (!map || !window.maptilersdk) return;

    map.innerHTML = "";
    const mt = window.maptilersdk;
    mt.config.apiKey = apiKey;

    const instance = new mt.Map({
      container: "map",
      style: mt.MapStyle.STREETS,
      center: [markers[0].lng, markers[0].lat],
      zoom: 5
    });

    const bounds = new mt.LngLatBounds();
    markers.forEach(marker => {
      const popup = new mt.Popup({ offset: 24 }).setHTML(
        `<strong>${marker.title}</strong><br>${marker.price}`
      );

      new mt.Marker({ color: "#ff5a5f" })
        .setLngLat([marker.lng, marker.lat])
        .setPopup(popup)
        .addTo(instance);

      bounds.extend([marker.lng, marker.lat]);
    });

    if (markers.length > 1) {
      instance.fitBounds(bounds, { padding: 60 });
    }
  }

  function wireCreateForm() {
    const form = document.getElementById("createPublicationForm");
    if (!form || form.dataset.bound === "true") return;

    form.dataset.bound = "true";

    const syncPanels = () => {
      const publisherMode = form.querySelector('input[name="publisherMode"]:checked')?.value || "Anonymous";
      const group = form.querySelector('select[name="group"]')?.value || "Inmuebles";

      form.querySelectorAll("[data-publisher-panel]").forEach(panel => {
        panel.style.display = panel.dataset.publisherPanel === publisherMode ? "" : "none";
      });

      form.querySelectorAll("[data-group-panel]").forEach(panel => {
        panel.style.display = panel.dataset.groupPanel === group ? "" : "none";
      });
    };

    form.addEventListener("change", event => {
      const target = event.target;
      if (!target) return;
      if (target.name === "publisherMode" || target.name === "group") {
        syncPanels();
      }
    });

    form.addEventListener("submit", async event => {
      event.preventDefault();
      const payload = serializeCreateForm(form);
      const feedback = document.getElementById("create-feedback");

      const response = await fetch("/api/content/create", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Requested-With": "fetch"
        },
        body: JSON.stringify(payload)
      });

      const result = await response.json();
      if (!response.ok) {
        if (feedback) {
          const errors = Array.isArray(result.errors) ? result.errors.join(" ") : "";
          feedback.innerHTML = `<div class="status-banner warning">${result.message || "No se pudo crear la publicación."} ${errors}</div>`;
        }
        return;
      }

      if (result.redirectUrl) {
        window.location.href = result.redirectUrl;
        return;
      }

      if (feedback) {
        feedback.innerHTML = `<div class="status-banner warning">Publicación creada. Guardá estos datos para darla de baja: ID <strong>${result.publicationId}</strong> · contraseña <strong>${result.anonymousPassword}</strong></div>`;
      }

      form.reset();
      const groupSelect = form.querySelector('select[name="group"]');
      if (groupSelect) groupSelect.value = payload.group || "Inmuebles";
      const anonymousRadio = form.querySelector('input[name="publisherMode"][value="Anonymous"]');
      if (anonymousRadio) anonymousRadio.checked = true;
      syncPanels();
    });

    syncPanels();
  }

  function serializeCreateForm(form) {
    const value = name => form.querySelector(`[name="${name}"]`)?.value ?? "";
    const checked = name => Boolean(form.querySelector(`[name="${name}"]`)?.checked);

    return {
      publisherMode: form.querySelector('input[name="publisherMode"]:checked')?.value || "Anonymous",
      group: value("group"),
      category: value("category"),
      title: value("title"),
      price: Number(value("price") || 0),
      currency: value("currency") || "USD",
      locality: value("locality"),
      shortDescription: value("shortDescription"),
      imagesCsv: value("imagesCsv"),
      longDescription: value("longDescription") || null,
      contactEmail: value("contactEmail") || null,
      contactName: value("contactName"),
      contactPhone: value("contactPhone"),
      featured: checked("featured"),
      videoUrl: value("videoUrl") || null,
      latitude: numberOrNull(value("latitude")),
      longitude: numberOrNull(value("longitude")),
      propertyType: value("propertyType") || null,
      operation: value("operation") || null,
      zone: value("zone") || null,
      totalAreaM2: numberOrNull(value("totalAreaM2")),
      coveredAreaM2: numberOrNull(value("coveredAreaM2")),
      roomsOrBedrooms: value("roomsOrBedrooms") || null,
      bathrooms: intOrNull(value("bathrooms")),
      address: value("address") || null,
      garageSpaces: intOrNull(value("garageSpaces")),
      ageYears: intOrNull(value("ageYears")),
      expenses: numberOrNull(value("expenses")),
      condition: value("condition") || null,
      mortgageEligible: checked("mortgageEligible"),
      professionalUseAllowed: checked("professionalUseAllowed"),
      services: value("services") || null,
      amenities: value("amenities") || null,
      vehicleType: value("vehicleType") || null,
      brand: value("brand") || null,
      model: value("model") || null,
      year: intOrNull(value("year")),
      kilometers: intOrNull(value("kilometers")),
      fuel: value("fuel") || null,
      transmission: value("transmission") || null,
      version: value("version") || null,
      color: value("color") || null,
      licensePlate: value("licensePlate") || null,
      engine: value("engine") || null,
      traction: value("traction") || null,
      doors: intOrNull(value("doors")),
      ownersCount: intOrNull(value("ownersCount")),
      acceptsTrade: checked("acceptsTrade"),
      financingAvailable: checked("financingAvailable"),
      equipment: value("equipment") || null,
      generalCondition: value("generalCondition") || null,
      subcategory: value("subcategory") || null,
      itemCondition: value("itemCondition") || null,
      sku: value("sku") || null,
      stock: intOrNull(value("stock")),
      measure: value("measure") || null,
      weight: value("weight") || null,
      dimensions: value("dimensions") || null,
      warranty: value("warranty") || null,
      shipping: value("shipping") || null,
      extraAttributesRaw: value("extraAttributesRaw") || null,
      accountEmail: value("accountEmail") || null,
      accountName: value("accountName") || null,
      accountPhone: value("accountPhone") || null,
      accountPassword: value("accountPassword") || null
    };
  }

  function numberOrNull(value) {
    return value === "" ? null : Number(value);
  }

  function intOrNull(value) {
    return value === "" ? null : parseInt(value, 10);
  }
})();
