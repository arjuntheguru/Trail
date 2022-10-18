import axios from "axios";

export default axios.create({
	//baseURL: `https://54644e8bab43.ngrok.io`
	//baseURL: `https://localhost:44305/`,
	//baseURL: 'https://localhost:8080'
	// baseURL: "http://164.68.116.13:8080/",
	baseURL: "https://localhost:5001/"
});
