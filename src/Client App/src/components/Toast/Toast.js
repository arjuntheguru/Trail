import { toast } from "react-toastify";

export const notifyError = (msg) => {
	toast.error(msg, {
		position: "top-center",
		autoClose: 3000,
		hideProgressBar: false,
		closeOnClick: true,
		pauseOnHover: true,
		bodyClassName: "text-center",
	});
};

export const notifySuccess = (msg) => {
	toast.info(msg, {
		position: "top-center",
		autoClose: 2000,
		hideProgressBar: false,
		closeOnClick: true,
		pauseOnHover: true,
		bodyClassName: "text-center",
	});
};
