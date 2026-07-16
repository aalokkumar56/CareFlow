const React = require("react");

const Icon = (props) => React.createElement("svg", { "data-testid": "phosphor-icon", ...props });

module.exports = new Proxy({}, { get: () => Icon });
