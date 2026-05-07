import syntaxHighlight from "@11ty/eleventy-plugin-syntaxhighlight";
import pluginRss from "@11ty/eleventy-plugin-rss";
import eleventyNavigationPlugin from "@11ty/eleventy-navigation";
import markdownIt from "markdown-it";
import markdownItAnchor from "markdown-it-anchor";

export default function(eleventyConfig) {
  const mdOptions = {
    html: true,
    breaks: false,
    linkify: true
  };

  const mdAnchorOptions = {
    permalink: markdownItAnchor.permalink.headerLink(),
    slugify: (s) => s.toLowerCase().replace(/\s+/g, '-').replace(/[^\w-]+/g, ''),
    level: [1, 2, 3, 4]
  };

  const md = markdownIt(mdOptions).use(markdownItAnchor, mdAnchorOptions);
  eleventyConfig.setLibrary("md", md);

  eleventyConfig.addPlugin(syntaxHighlight);
  eleventyConfig.addPlugin(pluginRss);
  eleventyConfig.addPlugin(eleventyNavigationPlugin);

  eleventyConfig.addPassthroughCopy("src/assets");
  eleventyConfig.addPassthroughCopy("src/robots.txt");
  eleventyConfig.addWatchTarget("src/assets/");

  eleventyConfig.addFilter("dateFormat", (dateObj) => {
    return new Date(dateObj).toLocaleDateString('en-US', {
      year: 'numeric', month: 'long', day: 'numeric'
    });
  });

  eleventyConfig.addFilter("isoDate", (dateObj) => new Date(dateObj).toISOString());
  eleventyConfig.addFilter("limit", (arr, limit) => arr.slice(0, limit));
  eleventyConfig.addFilter("capitalize", (str) => str ? str.charAt(0).toUpperCase() + str.slice(1) : '');
  eleventyConfig.addFilter("slugify", (str) => str ? str.toLowerCase().replace(/\s+/g, '-').replace(/[^\w-]+/g, '') : '');

  eleventyConfig.addShortcode("year", () => String(new Date().getFullYear()));

  return {
    dir: { input: "src", output: "_site", includes: "_includes", data: "_data" },
    templateFormats: ["md", "njk", "html"],
    markdownTemplateEngine: "njk",
    htmlTemplateEngine: "njk"
  };
}
