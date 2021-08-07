import React from "react";
import { Modal } from "react-bootstrap";
import SiteForm from "./SiteForm";

const AddSite = ({ show, onClose, pageNumber }) => {
	return (
		<Modal show={show} onHide={onClose} centered size="lg">
			
				{<SiteForm onClose={onClose} pageNumber={pageNumber} />}
			
		</Modal>
	);
};

export default AddSite;
