///<reference path="https://cdnjs.cloudflare.com/ajax/libs/jquery/3.1.0/jquery.min.js"/>
///<reference path="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/9.6.0/highlight.min.js"/>

// toast helper function
function feedability_toast(message) {
	$("#toaster").get(0).show({ text: message, duration: 3000 });
}

// url helper function
function feedability_url() {
	var url = location.protocol + '//' + location.hostname + (location.port ? ":" + location.port : "")
		+ location.pathname + (location.pathname.endsWith("/") ? "" : "/") 
		+ "api/fullfeed/" + $("#method").val()
		+ "?url=" + encodeURIComponent($("#url").val());
	if ($("#whitelist").get(0).value.trim() !== "")
		url = url + "&whitelist=" + encodeURIComponent($("#whitelist").val());
	if ($("#blacklist").get(0).value.trim() !== "")
		url = url + "&blacklist=" + encodeURIComponent($("#blacklist").val());
	return url;
}

// run API by calling controller
function feedability_run() {
	var method = $("#method").val();
	console.log(method);
	$("#loader").show();
	$.ajax(feedability_url(), { dataType: "text" })
		.done(function (data) {
			if (method === "Feed") {
				// display xml response
				$("#display")
					.addClass("feedability-code")
					.text(data);
				hljs.highlightBlock($("#display").get(0));
			} else if (method === "Article") {
				// render readable article
				$("#display")
					.empty()
					.removeClass("feedability-code")
					.append(data);
			}
		})
		.always(function () {
			$("#loader").hide();
		});
}

// clear cache by calling controller
function feedability_clear() {
	$.ajax("api/fullfeed/clearcache?url=" + encodeURIComponent($("#url").val()))
		.done(function () { feedability_toast("Cache cleared"); })
		.fail(function () { feedability_toast("Error clearing cache"); });
}

// copy Link to clipboard
function feedability_copy() {
	var linkText = $("#linkText").text(feedability_url()).show();
	window.getSelection().removeAllRanges();
	var range = document.createRange();
	range.selectNode(linkText.get(0));
	window.getSelection().addRange(range);
	document.execCommand("copy");
	window.getSelection().removeAllRanges();
	$("#linkText").text("").hide();

	console.log(feedability_url());
	feedability_toast("Link copied to clipboard");
}