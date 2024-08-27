window.addEventListener("load", initNetDbg);

function initNetDbg() {
	g_debugger = new NetDbg();
}

function NetDbg() {
	this.selection = -1;
	this.offsetX = -1;
	this.dragEvt = this.updateDrag.bind(this);
	this.dragStopEvt = this.stopDrag.bind(this);
	this.autoReconnect = true;

	document.getElementById("liveUpdate").checked = true;

	var showInterpolationDelay = document.getElementById("showInterpolationDelayLabel");
	var showTimeScale = document.getElementById("showTimeScaleLabel");
	var showRTT = document.getElementById("showRTTLabel")
	var showJitter = document.getElementById("showJitterLabel")
	var showCommandAge = document.getElementById("showCommandAgeLabel");
	var showSnapshotAge = document.getElementById("showSnapshotAgeLabel");
	var showInterpolationTimeScale = document.getElementById("showInterpolationTimeScaleLabel");
	showInterpolationDelay.style.backgroundColor = this.Colors[0];
	showTimeScale.style.backgroundColor = this.Colors[1];
	showRTT.style.backgroundColor = this.Colors[2];
	showJitter.style.backgroundColor = this.Colors[3];
	showCommandAge.style.backgroundColor = this.Colors[4];
	showSnapshotAge.style.backgroundColor = this.Colors[5];
	showInterpolationTimeScale.style.backgroundColor = this.Colors[6]


	/*var loader = new XMLHttpRequest();
	loader.dbg = this;
	loader.addEventListener("load", function(){this.dbg.loadContent(this.response);});
	loader.open("GET", "snapshots.json");
	loader.responseType = "json";
	loader.send();*/

	this.content = [];
	//this.connect("localhost:8787");

	this.pendingPresent = 0;
	this.pendingStats = 0;
	this.invalidate();

	// Auto-connect to the game on focus.
	document.onvisibilitychange = this.tryFastConnect.bind(this);
	window.onfocus = this.tryFastConnect.bind(this);
	this.tryFastConnect();
}

NetDbg.prototype.SnapshotWidth = 10;
NetDbg.prototype.SnapshotMargin = 2;
NetDbg.prototype.Colors = ['#e6194b', '#3cb44b', '#ffe119', '#4363d8', '#f58231',
    '#911eb4', '#46f0f0', '#f032e6', '#bcf60c', '#fabebe',
    '#008080', '#e6beff', '#9a6324', '#fffac8', '#800000',
    '#aaffc3', '#808000', '#ffd8b1', '#000075', '#808080'];


NetDbg.prototype.updateNames = function(nameList) {
	var connection = JSON.parse(nameList);
	var con = connection.index;
	if (this.content[con] == undefined) {
		var container = document.getElementById("connectionContainer");
		this.content[con] = {};
		this.content[con].container = document.createElement("div");
		if (con != 0)
			this.content[con].container.style.display = "none";
		var title = document.createElement("div");
		title.className = "ConnectionTitle";
		title.appendChild(document.createTextNode(connection.name));
		title.addEventListener("click", function(){
			if (this.nextElementSibling.style.display == "none") {
				this.nextElementSibling.style.display = "block";
				g_debugger.invalidate();
			} else
				this.nextElementSibling.style.display = "none";
			});
		container.appendChild(title);
		this.content[con].hasTimeData = false;
		this.content[con].maxPackets = 1;
		this.content[con].legend = document.createElement("div");
		this.content[con].legend.className = "LegendOverlay";
		this.content[con].container.appendChild(this.content[con].legend);
		this.content[con].canvas = document.createElement("canvas");
		this.content[con].ctx = this.content[con].canvas.getContext("2d");
		this.content[con].canvas.addEventListener("mousedown", this.startDrag.bind(this));
		this.content[con].container.appendChild(this.content[con].canvas);
		this.content[con].details = document.createElement("div");
		this.content[con].container.appendChild(this.content[con].details);
		container.appendChild(this.content[con].container);
		this.content[con].frames = [];
		this.content[con].names = [];
		this.content[con].errors = [];
		this.content[con].enabledErrors = [];
		this.content[con].totalError = [];
		this.content[con].totalErrorCount = [];
		this.content[con].total = [];
	}
	var legend = this.content[con].legend;
	for (var i = this.content[con].errors.length; i < connection.errors.length; ++i) {
		this.content[con].enabledErrors[i] = false;
		this.content[con].totalError[i] = 0;
		this.content[con].totalErrorCount[i] = 0;
	}

	for (var i = this.content[con].names.length; i < connection.ghosts.length; ++i) {
		this.content[con].total[i*2] = 0;
		this.content[con].total[i*2 + 1] = 0;
		var line = document.createElement("div");
		line.style.color = "white";
		line.style.padding = "2px";
		line.style.margin = "2px";
		line.style.borderWidth = "1px";
		line.style.borderColor = this.Colors[i%this.Colors.length];
		line.style.borderStyle = "solid";
		line.appendChild(document.createTextNode(connection.ghosts[i]));
		legend.appendChild(line);
	}
	this.content[con].names = connection.ghosts;
	this.content[con].errors = connection.errors;
}

NetDbg.prototype.invalidateLegendStats = function() {
	if (this.pendingStats)
		return;
	this.pendingStats = setTimeout(this.updateLegendStats.bind(this), 100);
}

NetDbg.prototype.updateLegendStats = function() {
	this.pendingStats = 0;
	for (var con = 0; con < this.content.length; ++con) {
		if (this.content[con] == undefined)
			continue;
		var legend = this.content[con].legend;
		var items = legend.children;
		for (var i = 0; i < this.content[con].names.length; ++i) {
			if (this.content[con].total[i*2] > 0) {
				var avgFrame = Math.round(this.content[con].total[i*2] / this.content[con].frames.length);
				var avgEnt = Math.round(this.content[con].total[i*2] / this.content[con].total[i*2 + 1]);
				items[i].firstChild.nodeValue = this.content[con].names[i] + ": " + avgFrame + " bits/frame, " + avgEnt + " bits/entity";
			}
		}
	}
}

NetDbg.prototype.tryFastConnect = function() {
	var isConnected = this.ws !== undefined && this.ws.readyState <= 1;
	if(document.visibilityState === "visible" && this.autoReconnect === true && !isConnected) {
		var connectDlgValue = document.getElementById('connectUIButtonValue').value;
		this.connect(connectDlgValue);
	}
}

NetDbg.prototype.connect = function(host) {

	console.log(`'${this.constructor.name}' connecting to websocket ${host}...`);

	document.getElementById('connectDlg').className = "NetDbgConnecting";

	// Connect to unity
	this.ws = new WebSocket("ws://" + host);
	this.ws.binaryType = "arraybuffer";
	this.ws.addEventListener("message", this.wsReceive.bind(this));
	this.ws.addEventListener("open", this.wsOpen.bind(this));
	this.ws.addEventListener("close", this.wsClose.bind(this));
	//this.ws.addEventListener("error", this.wsClose.bind(this));
}
NetDbg.prototype.disconnect = function() {
	this.ws.close();
	this.autoReconnect = false;
	document.getElementById('connectDlg').className = "NetDbgDisconnected";
	console.log(`'${this.constructor.name}' disconnected from '${this.ws.url}']!`)
}

NetDbg.prototype.wsOpen = function(evt) {
	this.autoReconnect = true;
	document.getElementById('connectDlg').className = "NetDbgConnected";
	console.log(`'${this.constructor.name}' successfully connected to '${this.ws.url}', resetting data!`)

	// Clear the existing data as we now have new data (i.e. a new run) to show.
	this.content = [];
	var container = document.getElementById("connectionContainer");
	while (container.firstChild)
		container.removeChild(container.firstChild);
	this.selection = -1;
	this.offsetX = -1;
	document.getElementById("liveUpdate").checked = true;
}

NetDbg.prototype.wsClose = function(evt) {
	document.getElementById('connectDlg').className = "NetDbgDisconnected";
	console.log(`'${this.constructor.name}' WebSocket '${this.ws.url}' closed with [${evt.code}:'${evt.reason}']!`)
}

NetDbg.prototype.wsReceive = function(evt) {

	if (typeof(evt.data) == "string") {
		this.updateNames(evt.data);
	} else {
		var tick = new Uint32Array(evt.data);
		var header = new Uint8Array(evt.data, 4);
		var con = header[0];
		var timeLen = header[1];
		var snapshotLen = header[2];
		var commandLen = header[3];
		var discardedPackets = header[5];

		var content = this.content[con];
		if(content === undefined) return;

		var dataOffset = 12;

		var time = [];
		var timeArr = new Float32Array(evt.data, dataOffset);
		for (var i = 0; i < timeLen; ++i) {
			time.push({
				fraction: timeArr[i*9],
				scale: timeArr[i*9 + 1],
				interpolation: timeArr[i*9 + 2],
				interpolationScale: timeArr[i*9 + 3],
				commandAge: timeArr[i*9 + 4],
				rtt: timeArr[i*9 + 5],
				jitter: timeArr[i*9 + 6],
				snapshotAgeMin: timeArr[i*9 + 7],
				snapshotAgeMax: timeArr[i*9 + 8]
			});
		}

		dataOffset += timeLen * 36;

		var snapTickArr = new Uint32Array(evt.data, dataOffset);
		var snapshotTicks = [];
		for (var i = 0; i < snapshotLen; ++i) {
			snapshotTicks.push(snapTickArr[i]);
		}

		dataOffset += snapshotLen * 4;

		var snapArr = new Uint32Array(evt.data, dataOffset);
		var snap = [];
		var totalSize = 0;

		for (var i = 0; i < content.names.length; ++i) {
			snap.push({count: snapArr[i*3], size: snapArr[i*3+1], uncompressed: snapArr[i*3+2]});
			content.total[i*2] += snapArr[i*3+1];
			content.total[i*2 + 1] += snapArr[i*3];
			totalSize += snapArr[i*3+1];
		}

		dataOffset += content.names.length * 3 * 4;

		var predictionArr = new Float32Array(evt.data, dataOffset);
		var predictionErr = [];
		for (var i = 0; i < content.errors.length; ++i)
		{
			if (predictionArr[i] > 0) {
				content.enabledErrors[i] = true;
				content.totalErrorCount[i] += 1;
				content.totalError[i] += predictionArr[i];
			}
			predictionErr.push(predictionArr[i]);
		}
		dataOffset += content.errors.length * 4;

		var cmdTickArr = new Uint32Array(evt.data, dataOffset);
		var commandTicks = [];
		for (var i = 0; i < commandLen; ++i) {
			commandTicks.push(cmdTickArr[i]);
		}

		var commandSize = cmdTickArr[commandLen];

		dataOffset += commandLen * 4 + 4;

		var snapshotAge = 0;
		if (snapshotLen > 0) {
			snapshotAge = tick[0] - snapTickArr[snapshotLen-1];
		} else if (content.frames.length > 0) {
			snapshotAge = content.frames[content.frames.length-1].snapshotAge + 1;
		}
		var maxPackets = Math.ceil(totalSize / (1400*8));
		if (maxPackets > content.maxPackets)
			content.maxPackets = maxPackets;
		if (timeLen > 0)
			content.hasTimeData = true;
		if (content.frames.length > 0) {
			var lastFrame = content.frames[content.frames.length-1];
			if (lastFrame.serverTick+1 < tick[0]) {
				var age = lastFrame.snapshotAge;
				var emptySnap = [];
				for (var i = 0; i < content.names.length; ++i) {
					emptySnap.push({count: 0, size: 0, uncompressed: 0});
				}
				for (var missing = lastFrame.serverTick + 1; missing < tick[0]; ++missing) {
					++age;
					content.frames.push({serverTick: missing, snapshotAge: age, snapshot: emptySnap, snapshotTicks: [], predictionError: [], time: [], commandTicks: [], commandSize: 0});
				}
			}
		}
		content.frames.push({serverTick: tick[0], snapshotAge: snapshotAge, snapshot: snap, snapshotTicks: snapshotTicks, predictionError: predictionErr, time: time, commandTicks: commandTicks, commandSize: commandSize, discardedPackets: discardedPackets});
		this.invalidate();
		this.invalidateLegendStats();
	}
}

NetDbg.prototype.loadContent = function(content) {
	this.content = content;
	this.invalidate();
}

NetDbg.prototype.startDrag = function(evt) {
	this.grabX = evt.clientX;
	this.dragStarted = false;
	document.addEventListener("mousemove", this.dragEvt);
	document.addEventListener("mouseup", this.dragStopEvt);
}
NetDbg.prototype.stopDrag = function(evt) {
	document.removeEventListener("mousemove", this.dragEvt);
	document.removeEventListener("mouseup", this.dragStopEvt);
	if (!this.dragStarted)
		this.select(evt);
}
NetDbg.prototype.updateDrag = function(evt) {
	if (!this.dragStarted && Math.abs(this.grabX - evt.clientX) > 3) {
		this.dragStarted = true;
		this.offsetX = this.currentOffset();
		document.getElementById("liveUpdate").checked = false;
	}
	if (this.dragStarted) {
		this.offsetX -= evt.clientX - this.grabX;
		this.grabX = evt.clientX;
		if (this.offsetX < 0)
			this.offsetX = 0;
		if (this.offsetX > this.maxOffset())
			this.offsetX = this.maxOffset();
	}
	this.invalidate();
}

NetDbg.prototype.toggleLiveUpdate = function(enable) {
	if (enable) {
		this.offsetX = -1;
		this.invalidate();
	} else
		this.offsetX = this.currentOffset();
}

NetDbg.prototype.createName = function(name) {
	var div = document.createElement("div");
	div.style.display = "inline-block";
	div.style.width = "0";
	div.style.whiteSpace = "nowrap";
	div.appendChild(document.createTextNode(name));
	return div;
}

NetDbg.prototype.createCount = function(count, uncompressed) {
	var div = document.createElement("div");
	div.style.display = "inline-block";
	div.style.width = "0";
	div.style.whiteSpace = "nowrap";
	div.style.marginLeft = "200px";
	div.appendChild(document.createTextNode("" + count + " (" + uncompressed + ")"));
	return div;
}
NetDbg.prototype.createSize = function(sizeBits, sizeBytes) {
	var div = document.createElement("div");
	div.style.display = "inline-block";
	div.style.width = "0";
	div.style.whiteSpace = "nowrap";
	div.style.marginLeft = "400px";
	div.appendChild(document.createTextNode("" + sizeBits + " (" + sizeBytes + ")"));
	return div;
}
NetDbg.prototype.createInstSize = function(sizeBits, sizeBytes) {
	var div = document.createElement("div");
	div.style.display = "inline-block";
	div.style.width = "0";
	div.style.whiteSpace = "nowrap";
	div.style.marginLeft = "200px";
	div.appendChild(document.createTextNode("" + sizeBits + " (" + sizeBytes + ")"));
	return div;
}
NetDbg.prototype.alternateColorHighlighting = function(element, index) {
	element.style.backgroundColor = index % 2 === 0 ? "white" : "#efefef";
}

NetDbg.prototype.select = function(evt) {
	var offset = evt.clientX;
	for (var p = evt.target; p; p = p.offsetParent) {
		offset -= p.offsetLeft;
	}
	offset += this.currentOffset();
	this.selection = Math.floor(offset / this.SnapshotWidth);

	for (var con = 0; con < this.content.length; ++con) {
		var content = this.content[con];
		if (content == undefined)
			continue;
		var descr = content.details;
		while (descr.firstChild)
			descr.removeChild(descr.firstChild);
		if (this.selection >= 0 && this.selection < content.frames.length) {
			var div = document.createElement("div");
			descr.appendChild(div);
			var tick = document.createElement("div");
			descr.appendChild(tick);
			var age = document.createElement("div");
			descr.appendChild(age);
			var snapshots = document.createElement("div");
			descr.appendChild(snapshots);
			var commands = document.createElement("div");
			descr.appendChild(commands);
			var discard = document.createElement("div");
			descr.appendChild(discard);
			var totalSize = 0;

			descr.appendChild(document.createElement("hr"));
			var headerDiv = document.createElement("div");
			headerDiv.style.fontWeight = "bold";
			var nameHead = this.createName("Ghost Type");
			headerDiv.appendChild(nameHead);

			var sizeHead = this.createSize("Total size bits", "bytes");
			headerDiv.appendChild(sizeHead);

			var countHead = this.createCount("Instances", "Uncompressed");
			headerDiv.appendChild(countHead);

			var isizeHead = this.createInstSize("Instance avg. size bits", "bytes");
			headerDiv.appendChild(isizeHead);

			descr.appendChild(headerDiv);
			for (var i = 0; i < content.frames[this.selection].snapshot.length; ++i) {
				var type = content.frames[this.selection].snapshot[i];
				if (type.count == 0)
					continue;

				var sectionDiv = document.createElement("div");
				this.alternateColorHighlighting(sectionDiv, i+1);

				var name = this.createName(content.names[i]);
				sectionDiv.appendChild(name);

				var size = this.createSize(type.size, Math.round(type.size / 8));
				sectionDiv.appendChild(size);

				var count = this.createCount(type.count, type.uncompressed);
				sectionDiv.appendChild(count);

				var isize = this.createInstSize(Math.round(type.size / type.count), Math.round(type.size / (8*type.count)));
				sectionDiv.appendChild(isize);

				descr.appendChild(sectionDiv);
				totalSize += type.size;
			}
			if (content.frames[this.selection].predictionError != undefined) {

				var errorCount = 0;
				var table = document.createElement("table");
				for (var err = 0; err < content.errors.length; ++err) {
					if (content.enabledErrors[err]) {
						errorCount++;
						var sectionTr = document.createElement("tr");
						this.alternateColorHighlighting(sectionTr, errorCount);

						var nameTd = document.createElement("td");
						nameTd.textContent = "" + content.errors[err];
						nameTd.style.minWidth = "200px";
						nameTd.style.padding = "0px 40px 0px 0px";
						sectionTr.appendChild(nameTd);

						var errorTd = document.createElement("td");
						errorTd.textContent = content.frames[this.selection].predictionError[err];
						nameTd.style.minWidth = "100px";
						errorTd.style.padding = "0px 40px 0px 0px";
						sectionTr.appendChild(errorTd);

						table.appendChild(sectionTr);
					}
				}

				// Only show the Prediction errors if we actually have some.
				if(errorCount !== 0)
				{
					descr.appendChild(document.createElement("hr"));

					var titleDiv = document.createElement("div");
					titleDiv.className = "DetailsTitle";
					titleDiv.style.fontWeight = "bold";
					titleDiv.appendChild(document.createTextNode("Prediction errors"));
					descr.appendChild(titleDiv);

					descr.appendChild(table);
				}
			}

			var avgCommandAge = 0;
			var avgTimeScale = 0;
			var avgInterpolation = 0;
			var avgInterpolationScale = 0;
			var avgRTT = 0;
			var avgJitter = 0;
			var avgSnapshotAgeMin = 0;
			var avgSnapshotAgeMax = 0;
			for (var t = 0; t < content.frames[this.selection].time.length; ++t) {
				avgCommandAge += content.frames[this.selection].time[t].commandAge / content.frames[this.selection].time.length;
				avgTimeScale += content.frames[this.selection].time[t].scale / content.frames[this.selection].time.length;
				avgInterpolation += content.frames[this.selection].time[t].interpolation / content.frames[this.selection].time.length;
				avgInterpolationScale += content.frames[this.selection].time[t].interpolationScale / content.frames[this.selection].time.length;
				avgRTT += content.frames[this.selection].time[t].rtt / content.frames[this.selection].time.length;
				avgJitter += content.frames[this.selection].time[t].jitter / content.frames[this.selection].time.length;
				avgSnapshotAgeMin += content.frames[this.selection].time[t].snapshotAgeMin / content.frames[this.selection].time.length;
				avgSnapshotAgeMax += content.frames[this.selection].time[t].snapshotAgeMax / content.frames[this.selection].time.length;
			}

			var titleText = "Network frame " + this.selection;
			var titleDiv = document.createElement("div");
			titleDiv.className = "DetailsTitle";
			titleDiv.style.fontWeight = "bold";
			titleDiv.appendChild(document.createTextNode(titleText));
			div.appendChild(titleDiv);

			var tickText = "Server tick " + content.frames[this.selection].serverTick;
			tickText += " (" + (this.selection>0?(content.frames[this.selection].serverTick - content.frames[this.selection-1].serverTick):0) + ")";
			tickText += " Time scale " + avgTimeScale.toFixed(2);
			tick.appendChild(document.createTextNode(tickText));

			//var ageText = "Snapshot age " + content.frames[this.selection].snapshotAge.toFixed(2);
			var ageText = "Snapshot age " + avgSnapshotAgeMin + " - " + avgSnapshotAgeMax;
			ageText += " Interpolation delay " + avgInterpolation.toFixed(2);
			ageText += " Interpolation Scale " + avgInterpolationScale.toFixed(2);
			ageText += " Command age " + avgCommandAge.toFixed(2);
			ageText += " RTT " + avgRTT.toFixed(2) + " +/- " + avgJitter.toFixed(2);
			age.appendChild(document.createTextNode(ageText));

			var snapText = "Snapshot ticks [";
			for (var i = 0; i < content.frames[this.selection].snapshotTicks.length; ++i) {
				snapText += i>0?", ":"" + content.frames[this.selection].snapshotTicks[i];
			}
			snapText += "] ";
			snapText += Math.round(totalSize / 8) + " bytes (" + totalSize + " bits)";
			snapshots.appendChild(document.createTextNode(snapText));

			var cmdText = "Command ticks [";
			for (var i = 0; i < content.frames[this.selection].commandTicks.length; ++i) {
				cmdText += i>0?", ":"" + content.frames[this.selection].commandTicks[i];
			}
			cmdText += "] " + content.frames[this.selection].commandSize + " bytes";
			commands.appendChild(document.createTextNode(cmdText));
			if (content.frames[this.selection].discardedPackets > 0) {
				discard.appendChild(document.createTextNode("Discarded " + content.frames[this.selection].discardedPackets + " packets"))
			}
		}
	}
	this.invalidate();
}

NetDbg.prototype.invalidate = function() {
	if (this.pendingPresent == 0)
		this.pendingPresent = requestAnimationFrame(this.present.bind(this));
}

NetDbg.prototype.currentOffset = function() {
	if (this.offsetX < 0)
		return this.maxOffset();
	return this.offsetX;
}
NetDbg.prototype.maxOffset = function() {
	var maxOffset = 0;
	for (var con = 0; con < this.content.length; ++con) {
		if (this.content[con] == undefined)
			continue;
		if (this.content[con].container.style.display == "none")
			continue;
		var ofs = this.content[con].frames.length * this.SnapshotWidth - this.content[con].container.offsetWidth;
		if (ofs > maxOffset)
			maxOffset = ofs;
	}
	return maxOffset;
}

NetDbg.prototype.present = function() {
	this.pendingPresent = 0;
	var showInterpolationDelay = document.getElementById("showInterpolationDelay").checked;
	var showPredictionErrors = document.getElementById("showPredictionErrors").checked;
	var showTimeScale = document.getElementById("showTimeScale").checked;
	var showRTT = document.getElementById("showRTT").checked
	var showJitter = document.getElementById("showJitter").checked
	var showCommandAge = document.getElementById("showCommandAge").checked;
	var showSnapshotAge = document.getElementById("showSnapshotAge").checked;
	var showInterpolationTimeScale = document.getElementById("showInterpolationTimeScale").checked;
	var defaultDtHeight = 0;
	if (showInterpolationDelay || showTimeScale || showRTT || showJitter || showCommandAge || showSnapshotAge || showInterpolationTimeScale)
		defaultDtHeight = 80;
	for (var con = 0; con < this.content.length; ++con) {
		var content = this.content[con];
		if (content == undefined)
			continue;
		if (content.container.style.display == "none")
			continue;

		var dtHeight = defaultDtHeight;
		if (!content.hasTimeData)
			dtHeight = 0;

		content.canvas.width = content.canvas.parentElement.offsetWidth;
		var snapshotContentHeight = 680;
		var snapshotHeight = (snapshotContentHeight - dtHeight)*3 / 4;
		var commandHeight = snapshotHeight / 3;

		var predictionContentHeight = 0;
		var predictionErrorHeight = 32;
		if (showPredictionErrors) {
			for (var i = 0; i < content.errors.length; ++i) {
				if (content.enabledErrors[i])
					predictionContentHeight += predictionErrorHeight;
			}
		}
		content.canvas.height = snapshotContentHeight + predictionContentHeight;

		var byteScale = 0.25 / (8 * content.maxPackets);

		content.ctx.fillStyle = "black";
		content.ctx.fillRect(0,0,content.canvas.width, content.canvas.height);

		content.ctx.fillStyle = "gray";
		for (var i = 1; i <= content.maxPackets; ++i) {
			content.ctx.fillRect(0,snapshotHeight - 8000*byteScale*i,content.canvas.width, 1);
			//content.ctx.fillRect(0,snapshotHeight+commandHeight - 8000*byteScale*i,content.canvas.width, 1);
		}
		if (dtHeight > 0)
			content.ctx.fillRect(0,snapshotHeight+commandHeight + dtHeight/2,content.canvas.width, 1);

		var currentOffset = this.currentOffset();

		if (this.selection >= 0 && this.selection < content.frames.length) {
			content.ctx.fillStyle = "#fc0fc0";
			content.ctx.fillRect(this.selection*this.SnapshotWidth-this.SnapshotMargin/2 - currentOffset, 0, this.SnapshotWidth, content.canvas.height);
		}


		for (var i = 0; i < content.frames.length; ++i) {
			var total = 0;
			var totalCount = 0;
			var totalUncompressed = 0;
			for (var t = 0; t < content.frames[i].snapshot.length; ++t) {
				content.ctx.fillStyle = this.Colors[t%this.Colors.length];
				content.ctx.fillRect(i*this.SnapshotWidth - currentOffset, snapshotHeight - byteScale * (total + content.frames[i].snapshot[t].size), this.SnapshotWidth-this.SnapshotMargin, byteScale * content.frames[i].snapshot[t].size);
				total += content.frames[i].snapshot[t].size;
				totalCount += content.frames[i].snapshot[t].count;
				totalUncompressed += content.frames[i].snapshot[t].uncompressed;
			}
			if (totalCount > 0) {
				var uncompressedAlpha = totalUncompressed / totalCount;
				// Highlight frames where > 10% of the items were uncompressed
				if (uncompressedAlpha > 0.1) {
					uncompressedAlpha = uncompressedAlpha * 0.5 + 0.5;
					content.ctx.strokeStyle = "rgba(255,0,0," + uncompressedAlpha + ")";
					content.ctx.strokeRect(i*this.SnapshotWidth - currentOffset, snapshotHeight-byteScale*total, this.SnapshotWidth-this.SnapshotMargin, byteScale*total);
				}
			}
			if (content.frames[i].commandSize > 0) {
				content.ctx.fillStyle = this.Colors[0];
				content.ctx.fillRect(i*this.SnapshotWidth - currentOffset, snapshotHeight+commandHeight - byteScale * content.frames[i].commandSize*8, this.SnapshotWidth-this.SnapshotMargin, byteScale * content.frames[i].commandSize*8);
			}
			if (content.frames[i].discardedPackets > 0) {
				content.ctx.fillStyle = "red";
				var xpos = i*this.SnapshotWidth-this.SnapshotMargin/2 - currentOffset;
				content.ctx.fillRect(xpos, 0, this.SnapshotWidth, content.frames[i].discardedPackets * 10);
			}

			// Vertical timeline lines, one for each snapshot tick.
			{
				content.ctx.strokeStyle = "gray";
				var xpos = i*this.SnapshotWidth - currentOffset;
				var ypos = snapshotHeight;
				// TODO: I'd like to show larger lines for the TickRate markers here (e.g. 60Hz), but it's not currently sent.
				var isHundredMarker = content.frames[i].serverTick % 100 === 0;
				var isTenTickMarker = content.frames[i].serverTick % 10 === 0;
				content.ctx.beginPath();
				content.ctx.lineTo(xpos, ypos);
				ypos += isHundredMarker ? 60 : (isTenTickMarker ? 30 : 20);
				content.ctx.lineTo(xpos, ypos);
				content.ctx.stroke();
			}

			if (showPredictionErrors) {
				var predictionErrorBase = snapshotContentHeight + predictionErrorHeight;
				if (content.frames[i].predictionError != undefined) {
					content.ctx.fillStyle = "blue";
					for (var err = 0; err < content.errors.length; ++err) {
						if (content.enabledErrors[err]) {
							var avgError = content.totalError[err] / content.totalErrorCount[err];
							// We target an average error to fill up 10%
							var size = content.frames[i].predictionError[err] * predictionErrorHeight * 0.1 / avgError;
							if (size > predictionErrorHeight-2)
								size = predictionErrorHeight-2;
							content.ctx.fillRect(i*this.SnapshotWidth - currentOffset, predictionErrorBase - size, this.SnapshotWidth-this.SnapshotMargin, size);
							predictionErrorBase += predictionErrorHeight;
						}
					}
				}
			}
		}

		if (showInterpolationDelay && content.hasTimeData) {
			content.ctx.beginPath();
			for (var i = 0; i < content.frames.length; ++i) {
				for (var frac = 0; frac < content.frames[i].time.length; ++frac) {
					var frameOffset = i + content.frames[i].time[frac].fraction;
					var xpos = frameOffset*this.SnapshotWidth-this.SnapshotMargin/2 - currentOffset;
					content.ctx.lineTo(xpos, snapshotHeight + commandHeight + dtHeight - content.frames[i].time[frac].interpolation*2);
				}
			}
			content.ctx.strokeStyle = this.Colors[0];
			content.ctx.stroke();
		}

		if (showTimeScale && content.hasTimeData) {
			content.ctx.beginPath();
			for (var i = 0; i < content.frames.length; ++i) {
				for (var frac = 0; frac < content.frames[i].time.length; ++frac) {
					var frameOffset = i + content.frames[i].time[frac].fraction;
					var xpos = frameOffset*this.SnapshotWidth-this.SnapshotMargin/2 - currentOffset;
					content.ctx.lineTo(xpos, snapshotHeight + commandHeight + dtHeight/2 - (content.frames[i].time[frac].scale-1)*10 * dtHeight/2);
				}
			}
			content.ctx.strokeStyle = this.Colors[1];
			content.ctx.stroke();
		}

		if (showInterpolationTimeScale && content.hasTimeData) {
			content.ctx.beginPath();
			for (var i = 0; i < content.frames.length; ++i) {
				for (var frac = 0; frac < content.frames[i].time.length; ++frac) {
					var frameOffset = i + content.frames[i].time[frac].fraction;
					var xpos = frameOffset*this.SnapshotWidth-this.SnapshotMargin/2 - currentOffset;
					content.ctx.lineTo(xpos, snapshotHeight + commandHeight + dtHeight/2 - (content.frames[i].time[frac].interpolationScale-1)*10 * dtHeight/2);
				}
			}
			content.ctx.strokeStyle = this.Colors[6];
			content.ctx.stroke();
		}

		if (showRTT && content.hasTimeData) {
			content.ctx.beginPath();
			for (var i = 0; i < content.frames.length; ++i) {
				for (var frac = 0; frac < content.frames[i].time.length; ++frac) {
					var frameOffset = i + content.frames[i].time[frac].fraction;
					var xpos = frameOffset*this.SnapshotWidth-this.SnapshotMargin/2 - currentOffset;
					content.ctx.lineTo(xpos, snapshotHeight + commandHeight + dtHeight - content.frames[i].time[frac].rtt * dtHeight / 500);
				}
			}
			content.ctx.strokeStyle = this.Colors[2];
			content.ctx.stroke();
		}

		if (showJitter && content.hasTimeData) {
			content.ctx.beginPath();
			for (var i = 0; i < content.frames.length; ++i) {
				for (var frac = 0; frac < content.frames[i].time.length; ++frac) {
					var frameOffset = i + content.frames[i].time[frac].fraction;
					var xpos = frameOffset*this.SnapshotWidth-this.SnapshotMargin/2 - currentOffset;
					content.ctx.lineTo(xpos, snapshotHeight + commandHeight + dtHeight - content.frames[i].time[frac].jitter * dtHeight / 50);
				}
			}
			content.ctx.strokeStyle = this.Colors[3];
			content.ctx.stroke();
		}

		if (showCommandAge && content.hasTimeData) {
			content.ctx.beginPath();
			for (var i = 0; i < content.frames.length; ++i) {
				for (var frac = 0; frac < content.frames[i].time.length; ++frac) {
					var frameOffset = i + content.frames[i].time[frac].fraction;
					var xpos = frameOffset*this.SnapshotWidth-this.SnapshotMargin/2 - currentOffset;
					content.ctx.lineTo(xpos, snapshotHeight + commandHeight + dtHeight/2 - content.frames[i].time[frac].commandAge*10);
				}
			}
			content.ctx.strokeStyle = this.Colors[4];
			content.ctx.stroke();
		}
		if (showSnapshotAge && content.hasTimeData) {
			content.ctx.beginPath();
			for (var i = 0; i < content.frames.length; ++i) {
				for (var frac = 0; frac < content.frames[i].time.length; ++frac) {
					var frameOffset = i + content.frames[i].time[frac].fraction;
					var xpos = frameOffset*this.SnapshotWidth-this.SnapshotMargin/2 - currentOffset;
					content.ctx.lineTo(xpos, snapshotHeight + commandHeight + dtHeight - content.frames[i].time[frac].snapshotAgeMin*2);
				}
			}
			content.ctx.strokeStyle = this.Colors[5];
			content.ctx.stroke();
			content.ctx.beginPath();
			for (var i = 0; i < content.frames.length; ++i) {
				for (var frac = 0; frac < content.frames[i].time.length; ++frac) {
					var frameOffset = i + content.frames[i].time[frac].fraction;
					var xpos = frameOffset*this.SnapshotWidth-this.SnapshotMargin/2 - currentOffset;
					content.ctx.lineTo(xpos, snapshotHeight + commandHeight + dtHeight - content.frames[i].time[frac].snapshotAgeMax*2);
				}
			}
			content.ctx.strokeStyle = this.Colors[5];
			content.ctx.stroke();
		}
		/*if (showSnapshotAge && content.hasTimeData) {
			content.ctx.beginPath();
			for (var i = 0; i < content.frames.length; ++i) {
				var frameOffset = i + 1;
				var xpos = frameOffset*this.SnapshotWidth-this.SnapshotMargin/2 - currentOffset;
				content.ctx.lineTo(xpos, snapshotHeight + commandHeight + dtHeight - content.frames[i].snapshotAge*2);
			}
			content.ctx.strokeStyle = this.Colors[5];
			content.ctx.stroke();
		}*/
		content.ctx.fillStyle = "white";
		content.ctx.fillRect(0,snapshotHeight,content.canvas.width, 2);
		content.ctx.fillRect(0,snapshotHeight+commandHeight,content.canvas.width, 2);

		if (showPredictionErrors) {
			predictionContentHeight = 0;
			content.ctx.font = '10px serif';
			for (var i = 0; i < content.errors.length; ++i) {
				if (content.enabledErrors[i]) {
					content.ctx.fillText(content.errors[i], 5, snapshotContentHeight+predictionContentHeight + 15);
					predictionContentHeight += predictionErrorHeight;
					content.ctx.fillRect(0,snapshotContentHeight+predictionContentHeight,content.canvas.width, 2);
				}
			}
		}
	}
}
