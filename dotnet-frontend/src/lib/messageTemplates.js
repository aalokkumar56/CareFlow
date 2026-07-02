export const renderMessageTemplate = (template, values = {}) => {
  if (!template) return "";
  return template.replace(/\{(\w+)\}/gi, (_, key) => values[key.toLowerCase()] ?? values[key] ?? "");
};
